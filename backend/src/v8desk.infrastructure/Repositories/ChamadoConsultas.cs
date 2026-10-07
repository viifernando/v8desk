using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using v8desk.domain.Entities;
using v8desk.domain.Enums;
using v8desk.domain.ValueObjects;
using v8desk.infrastructure.Persistence;
using v8desk.application.Abstractions;
using v8desk.domain.Exceptions;
using v8desk.application.Chamados;

namespace v8desk.infrastructure.Repositories;

// Permissões verificadas contra vínculos e membros persistidos, dentro do mesmo snapshot da leitura.
public sealed class ChamadoConsultas(V8DeskDbContext db, IUsuarioAtual identidade) : IChamadoConsultas
{
    public const int LimiteContagem = 10_000;

    private static readonly StatusChamado[] StatusEmAberto =
    [
        StatusChamado.AguardandoTriagem, StatusChamado.Aceito,
        StatusChamado.EmAtendimento, StatusChamado.AguardandoInformacao
    ];

    public async Task<IReadOnlyList<ChamadoResumo>> ListarFilaAsync(Guid filaId,
        CursorChamados? depois = null, int tamanho = 50, CancellationToken cancellationToken = default,
        Guid? categoriaId = null)
    {
        ValidarTamanho(tamanho);
        return await LerAsync(async () =>
        {
            var consulta = await ConsultaAutorizadaAsync(filaId, cancellationToken);
            if (categoriaId is { } categoria)
                consulta = FiltrarCategoria(consulta, categoria);
            if (depois is not null)
                consulta = consulta.Where(x => x.AbertoEm > depois.AbertoEm ||
                    (x.AbertoEm == depois.AbertoEm && x.Id.CompareTo(depois.Id) > 0));
            return (IReadOnlyList<ChamadoResumo>)await consulta.OrderBy(x => x.AbertoEm).ThenBy(x => x.Id).Take(tamanho)
                .Select(x => new ChamadoResumo(x.Id, x.Titulo, x.Status, x.Prioridade, x.AbertoEm,
                    EF.Property<Guid?>(x, "ResponsavelConsultaId"))).ToListAsync(cancellationToken);
        }, cancellationToken);
    }

    public async Task<PaginaChamados> PesquisarAsync(FiltroChamados filtro, string? cursor = null,
        int tamanho = 25, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filtro);
        ValidarTamanho(tamanho);
        var marcador = cursor is null ? null : LerCursor(cursor, filtro.Ordem);
        return await LerAsync(async () =>
        {
            var consulta = Filtrar(await ConsultaAutorizadaAsync(filtro.FilaId, cancellationToken), filtro);
            var contados = await consulta.Take(LimiteContagem + 1).CountAsync(cancellationToken);
            var pagina = await Paginar(consulta, filtro.Ordem, marcador, tamanho).ToListAsync(cancellationToken);
            var temMais = pagina.Count > tamanho;
            var itens = temMais ? pagina.Take(tamanho).ToList() : pagina;
            return new PaginaChamados(itens, temMais ? EscreverCursor(filtro.Ordem, itens[^1]) : null,
                Math.Min(contados, LimiteContagem), contados > LimiteContagem);
        }, cancellationToken);
    }

    internal IQueryable<ChamadoLinha> Paginar(IQueryable<Chamado> consulta, OrdemChamados ordem, Marcador? marcador, int tamanho) =>
        Ordenar(AposMarcador(consulta, ordem, marcador), ordem)
            .Take(tamanho + 1)
            .Select(x => new ChamadoLinha(x.Id, x.Numero, x.Titulo, x.Status, x.Prioridade, x.Visibilidade,
                x.AbertoEm, x.AtualizadoEm, x.ProximoVencimentoEm,
                db.Usuarios.Where(u => u.Id == x.SolicitanteId).Select(u => u.Nome).FirstOrDefault() ?? "",
                x.CategoriaAtual, x.ContextoAtual));

    internal IQueryable<Chamado> Filtrar(IQueryable<Chamado> consulta, FiltroChamados filtro)
    {
        consulta = filtro.Situacao switch
        {
            SituacaoListagem.EmAberto => consulta.Where(x => StatusEmAberto.Contains(x.Status)),
            SituacaoListagem.AguardandoValidacao => consulta.Where(x => x.Status == StatusChamado.Resolvido),
            SituacaoListagem.Encerrados => consulta.Where(x => x.Status == StatusChamado.Encerrado || x.Status == StatusChamado.Cancelado),
            _ => consulta
        };
        if (filtro.Prioridades is { Count: > 0 } prioridades)
        {
            var lista = prioridades.Distinct().ToArray();
            consulta = consulta.Where(x => lista.Contains(x.Prioridade));
        }
        var usuarioId = identidade.UsuarioId;
        consulta = filtro.Responsavel switch
        {
            FiltroResponsavel.Eu => consulta.Where(x => EF.Property<Guid?>(x, "ResponsavelConsultaId") == usuarioId),
            FiltroResponsavel.SemResponsavel => consulta.Where(x => EF.Property<Guid?>(x, "ResponsavelConsultaId") == null),
            FiltroResponsavel.Especifico when filtro.ResponsavelId is { } responsavel =>
                consulta.Where(x => EF.Property<Guid?>(x, "ResponsavelConsultaId") == responsavel),
            FiltroResponsavel.Especifico => throw new ValidacaoDominioException("responsavel_obrigatorio",
                nameof(FiltroChamados.ResponsavelId), "Escolha o responsável para filtrar."),
            _ => consulta
        };
        if (filtro.CategoriaId is { } categoria)
            consulta = FiltrarCategoria(consulta, categoria);
        if (filtro.AbertoDesde is { } desde)
            consulta = consulta.Where(x => x.AbertoEm >= desde);
        if (filtro.AbertoAte is { } ate)
            consulta = consulta.Where(x => x.AbertoEm < ate);
        if (!string.IsNullOrWhiteSpace(filtro.Texto))
        {
            var texto = filtro.Texto.Trim();
            if (texto.Length > 200)
                throw new ValidacaoDominioException("busca_longa", nameof(FiltroChamados.Texto), "Use até 200 caracteres na busca.");
            if (long.TryParse(texto.TrimStart('#'), out var numero))
                consulta = consulta.Where(x => x.Numero == numero);
            else
            {
                var padrao = "%" + texto.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
                consulta = consulta.Where(x => EF.Functions.ILike(x.Titulo, padrao, "\\"));
            }
        }
        return consulta;
    }

    private static IQueryable<Chamado> FiltrarCategoria(IQueryable<Chamado> consulta, Guid categoria)
    {
        var caminhoContendo = JsonSerializer.Serialize(new[] { categoria });
        return consulta.Where(x => EF.Functions.JsonContains(EF.Property<string>(x, "CategoriaCaminhoIds"), caminhoContendo));
    }

    private static IOrderedQueryable<Chamado> Ordenar(IQueryable<Chamado> consulta, OrdemChamados ordem) => ordem switch
    {
        OrdemChamados.MaisRecentes => consulta.OrderByDescending(x => x.AbertoEm).ThenByDescending(x => x.Id),
        OrdemChamados.MaisAntigos => consulta.OrderBy(x => x.AbertoEm).ThenBy(x => x.Id),
        OrdemChamados.AtualizadosRecentemente => consulta.OrderByDescending(x => x.AtualizadoEm).ThenByDescending(x => x.Id),
        OrdemChamados.Prioridade => consulta
            .OrderBy(x => x.Prioridade == Prioridade.Urgente ? 0 : x.Prioridade == Prioridade.Media ? 1 : 2)
            .ThenBy(x => x.AbertoEm).ThenBy(x => x.Id),
        _ => consulta.OrderBy(x => x.ProximoVencimentoEm).ThenBy(x => x.Id)
    };

    private static IQueryable<Chamado> AposMarcador(IQueryable<Chamado> consulta, OrdemChamados ordem, Marcador? m)
    {
        if (m is null) return consulta;
        var id = m.Id;
        switch (ordem)
        {
            case OrdemChamados.MaisRecentes:
            {
                var data = m.Data!.Value;
                return consulta.Where(x => x.AbertoEm < data || (x.AbertoEm == data && x.Id.CompareTo(id) < 0));
            }
            case OrdemChamados.MaisAntigos:
            {
                var data = m.Data!.Value;
                return consulta.Where(x => x.AbertoEm > data || (x.AbertoEm == data && x.Id.CompareTo(id) > 0));
            }
            case OrdemChamados.AtualizadosRecentemente:
            {
                var data = m.Data!.Value;
                return consulta.Where(x => x.AtualizadoEm < data || (x.AtualizadoEm == data && x.Id.CompareTo(id) < 0));
            }
            case OrdemChamados.Prioridade:
            {
                var data = m.Data!.Value;
                var nivel = m.Nivel!.Value;
                return consulta.Where(x =>
                    (x.Prioridade == Prioridade.Urgente ? 0 : x.Prioridade == Prioridade.Media ? 1 : 2) > nivel ||
                    ((x.Prioridade == Prioridade.Urgente ? 0 : x.Prioridade == Prioridade.Media ? 1 : 2) == nivel &&
                     (x.AbertoEm > data || (x.AbertoEm == data && x.Id.CompareTo(id) > 0))));
            }
            default:
            {
                if (m.Data is not { } vencimento)
                    return consulta.Where(x => x.ProximoVencimentoEm == null && x.Id.CompareTo(id) > 0);
                return consulta.Where(x => x.ProximoVencimentoEm == null || x.ProximoVencimentoEm > vencimento ||
                    (x.ProximoVencimentoEm == vencimento && x.Id.CompareTo(id) > 0));
            }
        }
    }

    internal sealed record Marcador(OrdemChamados Ordem, DateTimeOffset? Data, int? Nivel, Guid Id);

    internal static string EscreverCursor(OrdemChamados ordem, ChamadoLinha ultimo)
    {
        var marcador = ordem switch
        {
            OrdemChamados.MaisRecentes or OrdemChamados.MaisAntigos => new Marcador(ordem, ultimo.AbertoEm, null, ultimo.Id),
            OrdemChamados.AtualizadosRecentemente => new Marcador(ordem, ultimo.AtualizadoEm, null, ultimo.Id),
            OrdemChamados.Prioridade => new Marcador(ordem, ultimo.AbertoEm,
                ultimo.Prioridade switch { Prioridade.Urgente => 0, Prioridade.Media => 1, _ => 2 }, ultimo.Id),
            _ => new Marcador(ordem, ultimo.ProximoVencimentoEm, null, ultimo.Id)
        };
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(marcador)))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    internal static Marcador LerCursor(string cursor, OrdemChamados ordem)
    {
        try
        {
            var base64 = cursor.Replace('-', '+').Replace('_', '/');
            base64 = base64.PadRight(base64.Length + (4 - base64.Length % 4) % 4, '=');
            var marcador = JsonSerializer.Deserialize<Marcador>(Convert.FromBase64String(base64));
            var dataObrigatoria = ordem != OrdemChamados.PrazoMaisProximo;
            if (marcador is null || marcador.Ordem != ordem || marcador.Id == Guid.Empty
                || (dataObrigatoria && marcador.Data is null)
                || (ordem == OrdemChamados.Prioridade && marcador.Nivel is not (>= 0 and <= 2)))
                throw new FormatException();
            return marcador;
        }
        catch (Exception erro) when (erro is FormatException or JsonException or ArgumentException)
        {
            throw new ValidacaoDominioException("cursor_invalido", "cursor",
                "A página solicitada não é mais válida. Volte para a primeira página.");
        }
    }

    private static void ValidarTamanho(int tamanho)
    {
        if (tamanho is < 1 or > 100)
            throw new ValidacaoDominioException("pagina_invalida", nameof(tamanho), "Use páginas de 1 a 100 chamados.");
    }

    private async Task<T> LerAsync<T>(Func<Task<T>> leitura, CancellationToken cancellationToken)
    {
        if (db.Database.CurrentTransaction is not null)
            throw new InvalidOperationException("A consulta controla sua transação de leitura.");
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead, cancellationToken);
            var resultado = await leitura();
            await tx.CommitAsync(cancellationToken);
            return resultado;
        });
    }

    private async Task<IQueryable<Chamado>> ConsultaAutorizadaAsync(Guid filaId, CancellationToken cancellationToken)
    {
        var usuarioId = identidade.UsuarioId;
        if (!await db.Usuarios.AnyAsync(x => x.Id == usuarioId && x.Ativo, cancellationToken))
            throw new AcessoNegadoException("Seu usuário não está ativo nesta empresa.");
        var fila = await db.Filas.AsNoTracking().Include(x => x.Membros.Where(m => m.UsuarioId == usuarioId))
            .SingleOrDefaultAsync(x => x.Id == filaId, cancellationToken);
        if (fila is null) throw new AcessoNegadoException("A fila não está disponível nesta empresa.");
        var setorAtivo = await db.Setores.AnyAsync(x => x.Id == fila.SetorId && x.Ativo, cancellationToken);
        var podeAtenderFila = setorAtivo && fila.PodeAtender(usuarioId);
        var podeAcessarRestritos = setorAtivo && fila.PodeAcessarRestrito(usuarioId);
        var setores = await db.Vinculos.Where(x => x.Ativo && EF.Property<Guid>(x, "UsuarioPersistidoId") == usuarioId)
            .Where(x => db.Setores.Any(s => s.Id == x.SetorId && s.Ativo))
            .Select(x => x.SetorId).ToArrayAsync(cancellationToken);
        var consulta = db.Chamados.AsNoTracking().Where(x => EF.Property<Guid>(x, "FilaConsultaId") == filaId);
        if (!podeAtenderFila)
            consulta = consulta.Where(x => x.SolicitanteId == usuarioId ||
                (x.Visibilidade == Visibilidade.CompartilhadoComSetor &&
                 setores.Contains(EF.Property<Guid>(x, "SetorOrigemConsultaId"))));
        else if (!podeAcessarRestritos)
            consulta = consulta.Where(x => x.Visibilidade == Visibilidade.CompartilhadoComSetor || x.SolicitanteId == usuarioId);
        return consulta;
    }
}
