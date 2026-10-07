using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using v8desk.application.Abstractions;
using v8desk.application.Chamados;
using v8desk.application.Seguranca;
using v8desk.domain.Enums;
using v8desk.domain.Exceptions;
using v8desk.infrastructure.Persistence;

namespace v8desk.infrastructure.Repositories;

public sealed class ProntuarioConsultas(V8DeskDbContext db, IAcessoRepository acessos,
    IUsuarioAtual usuario) : IProntuarioConsultas
{
    public Task<ChamadoDetalhe> ObterAsync(Guid id, CancellationToken ct = default) => LerAsync(async () =>
    {
        await AutorizarAsync(id, ct);
        return await ConsultarDetalhe(id).SingleAsync(ct);
    }, ct);

    public Task<PaginaMensagens> MensagensAsync(Guid id, string? cursor, int tamanho, CancellationToken ct = default)
    {
        ValidarTamanho(tamanho);
        var marcador = cursor is null ? null : LerCursor(cursor, id);
        return LerAsync(async () =>
        {
            var atendente = await AutorizarAsync(id, ct);
            var pagina = await ConsultarMensagens(id, atendente, marcador, tamanho).ToListAsync(ct);
            var mais = pagina.Count > tamanho;
            var itens = pagina.Take(tamanho).ToArray();
            return new PaginaMensagens(itens.Select(m => new MensagemLinha(m.Id, m.AutorId, m.Tipo,
                m.TextoAtual, m.CriadaEm, m.Correcoes.Count > 0, m.Anexos.Count)).ToArray(),
                mais ? Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(
                    new Marcador(id, itens[^1].CriadaEm, itens[^1].Id)))) : null);
        }, ct);
    }

    public Task<PaginaEventos> EventosAsync(Guid id, long depois, int tamanho, CancellationToken ct = default)
    {
        ValidarTamanho(tamanho);
        if (depois < 0) throw new ValidacaoDominioException("cursor_invalido", "depois", "Use uma sequência de histórico válida.");
        return LerAsync(async () =>
        {
            var atendente = await AutorizarAsync(id, ct);
            var pagina = await ConsultarEventos(id, atendente, depois, tamanho).ToListAsync(ct);
            var itens = pagina.Take(tamanho).ToArray();
            return new PaginaEventos(itens, pagina.Count > tamanho ? itens[^1].Sequencia : null);
        }, ct);
    }

    private async Task<bool> AutorizarAsync(Guid id, CancellationToken ct)
    {
        var referencia = await acessos.ObterReferenciaAsync(id, ct)
            ?? throw new AcessoNegadoException("O chamado não está disponível para sua conta.");
        var dados = await acessos.ObterDadosAsync(usuario.UsuarioId, [referencia.FilaAtualId], ct)
            ?? throw new AcessoNegadoException("Sua conta não está disponível.");
        if (dados.Usuario.Id != usuario.UsuarioId)
            throw new AcessoNegadoException("Sua conta não está disponível.");
        var autorizacao = new AutorizacaoChamado(db.EmpresaId, dados);
        autorizacao.ExigirVisualizacao(referencia);
        return autorizacao.PodeAtender(referencia);
    }

    private async Task<T> LerAsync<T>(Func<Task<T>> leitura, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is not null) throw new InvalidOperationException("Use um escopo novo para a consulta.");
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead, ct);
            var resultado = await leitura();
            await tx.CommitAsync(ct);
            return resultado;
        });
    }
    internal IQueryable<ChamadoDetalhe> ConsultarDetalhe(Guid id) => db.Chamados.AsNoTracking().Where(c => c.Id == id)
        .Select(c => new ChamadoDetalhe(c.Id, c.Numero, c.Titulo, c.DescricaoOriginal, c.SolicitanteId,
            c.Status, c.Prioridade, c.Visibilidade, c.SetorOrigemNaAbertura, c.CategoriaAtual, c.ContextoAtual,
            c.AbertoEm, c.AtualizadoEm, c.ProximoVencimentoEm, c.LimiteValidacao, c.LimiteReabertura, c.Versao));

    internal IQueryable<v8desk.domain.Entities.Mensagem> ConsultarMensagens(Guid id, bool atendente, Marcador? marcador, int tamanho)
    {
        var query = db.Mensagens.AsNoTracking().Where(m => EF.Property<Guid?>(m, "ChamadoId") == id);
        if (!atendente) query = query.Where(m => m.Tipo == TipoMensagem.Publica);
        if (marcador is not null)
            query = query.Where(m => m.CriadaEm > marcador.Data || m.CriadaEm == marcador.Data && m.Id.CompareTo(marcador.Id) > 0);
        return query.OrderBy(m => m.CriadaEm).ThenBy(m => m.Id).Take(tamanho + 1);
    }

    internal IQueryable<EventoLinha> ConsultarEventos(Guid id, bool atendente, long depois, int tamanho)
    {
        var query = db.Eventos.AsNoTracking().Where(e => e.ChamadoId == id && e.Sequencia > depois);
        // Histórico externo expõe somente cabeçalhos; nunca os dicionários livres do prontuário.
        if (!atendente) query = query.Where(e => e.Tipo != TipoEventoChamado.NotaInternaAdicionada && e.Tipo != TipoEventoChamado.MensagemCorrigida);
        return query.OrderBy(e => e.Sequencia).Take(tamanho + 1)
            .Select(e => new EventoLinha(e.Sequencia, e.OcorridoEm, e.AutorId, e.Tipo));
    }

    internal sealed record Marcador(Guid ChamadoId, DateTimeOffset Data, Guid Id);
    private static Marcador LerCursor(string cursor, Guid chamadoId)
    {
        try
        {
            if (cursor.Length > 4096) throw new FormatException();
            var marcador = JsonSerializer.Deserialize<Marcador>(Convert.FromBase64String(cursor));
            if (marcador is null || marcador.ChamadoId != chamadoId || marcador.Id == Guid.Empty || marcador.Data == default)
                throw new FormatException();
            return marcador;
        }
        catch (Exception ex) when (ex is JsonException or FormatException or ArgumentException)
        { throw new ValidacaoDominioException("cursor_invalido", "cursor", "A página solicitada não é válida. Volte para a primeira página."); }
    }
    private static void ValidarTamanho(int tamanho)
    {
        if (tamanho is < 1 or > 100)
            throw new ValidacaoDominioException("pagina_invalida", "tamanho", "Use páginas de 1 a 100 registros.");
    }
}
