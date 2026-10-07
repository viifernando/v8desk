using Microsoft.EntityFrameworkCore;
using v8desk.domain.Enums;
using v8desk.infrastructure.Persistence;

namespace v8desk.infrastructure.Repositories;

public sealed record ChamadoResumo(Guid Id, string Titulo, StatusChamado Status, Prioridade Prioridade,
    DateTimeOffset AbertoEm, Guid? ResponsavelId);
public sealed record CursorChamados(DateTimeOffset AbertoEm, Guid Id);

// A autorização de negócio precisa fornecer a fila autorizada e IDs do usuário/setor
// a partir da identidade autenticada. O filtro de empresa é adicional a essa autorização.
public sealed class ChamadoConsultas(V8DeskDbContext db)
{
    public async Task<IReadOnlyList<ChamadoResumo>> ListarFilaAsync(Guid filaId, Guid usuarioId,
        Guid setorOrigemId, bool podeAtenderFila, bool podeAcessarRestritos,
        CursorChamados? depois = null, int tamanho = 50, CancellationToken cancellationToken = default)
    {
        if (tamanho is < 1 or > 100) throw new v8desk.domain.Exceptions.ValidacaoDominioException("pagina_invalida", nameof(tamanho), "Use páginas de 1 a 100 chamados.");
        var consulta = db.Chamados.Where(x => EF.Property<Guid>(x, "FilaConsultaId") == filaId);
        if (!podeAtenderFila)
            consulta = consulta.Where(x => x.SolicitanteId == usuarioId ||
                (x.Visibilidade == Visibilidade.CompartilhadoComSetor &&
                 EF.Property<Guid>(x, "SetorOrigemConsultaId") == setorOrigemId));
        else if (!podeAcessarRestritos)
            consulta = consulta.Where(x => x.Visibilidade == Visibilidade.CompartilhadoComSetor || x.SolicitanteId == usuarioId);
        if (depois is not null)
            consulta = consulta.Where(x => x.AbertoEm > depois.AbertoEm ||
                (x.AbertoEm == depois.AbertoEm && x.Id.CompareTo(depois.Id) > 0));
        return await consulta.OrderBy(x => x.AbertoEm).ThenBy(x => x.Id).Take(tamanho)
            .Select(x => new ChamadoResumo(x.Id, x.Titulo, x.Status, x.Prioridade, x.AbertoEm,
                EF.Property<Guid?>(x, "ResponsavelConsultaId"))).ToListAsync(cancellationToken);
    }
}
