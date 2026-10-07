using Microsoft.EntityFrameworkCore;
using v8desk.application.Abstractions;
using v8desk.domain.Entities;
using v8desk.infrastructure.Persistence;

namespace v8desk.infrastructure.Repositories;

public sealed class AcessoRepository(V8DeskDbContext db) : IAcessoRepository
{
    public Task<Usuario?> ObterUsuarioAsync(Guid id, CancellationToken cancellationToken = default) =>
        db.Usuarios.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
    public Task<ReferenciaAcessoChamado?> ObterReferenciaAsync(Guid chamadoId, CancellationToken cancellationToken = default) =>
        db.Chamados.AsNoTracking().Where(c => c.Id == chamadoId)
            .Select(c => new ReferenciaAcessoChamado(c.Id, c.EmpresaId, c.SolicitanteId,
                EF.Property<Guid>(c, "SetorOrigemConsultaId"), EF.Property<Guid>(c, "SetorConsultaId"),
                EF.Property<Guid>(c, "FilaConsultaId"), c.Visibilidade)).SingleOrDefaultAsync(cancellationToken);
    public async Task<DadosAcesso?> ObterDadosAsync(Guid usuarioId, IReadOnlyCollection<Guid> filas, CancellationToken cancellationToken = default)
    {
        var usuario = await ObterUsuarioAsync(usuarioId, cancellationToken);
        if (usuario is null) return null;
        var vinculos = await db.Vinculos.AsNoTracking().Where(v => EF.Property<Guid>(v, "UsuarioPersistidoId") == usuarioId).ToListAsync(cancellationToken);
        var setores = await db.Setores.AsNoTracking().Select(s => new SetorAcesso(s.Id, s.Ativo)).ToListAsync(cancellationToken);
        var ids = filas.Distinct().ToArray();
        var carregadas = ids.Length == 0 ? [] : await db.Filas.AsNoTrackingWithIdentityResolution()
            .Where(f => ids.Contains(f.Id)).Include(f => f.Membros).ToListAsync(cancellationToken);
        return new(usuario, vinculos, setores, carregadas);
    }
}
