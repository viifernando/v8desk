using Microsoft.EntityFrameworkCore;
using v8desk.application.Abstractions;
using v8desk.domain.Entities;
using v8desk.infrastructure.Persistence;

namespace v8desk.infrastructure.Repositories;

public sealed class ConfiguracaoRepository(V8DeskDbContext db) : IConfiguracaoRepository
{
    public void Adicionar(Setor setor) => db.Setores.Add(setor);
    public void Adicionar(VinculoSetor vinculo) => db.Vinculos.Add(vinculo);
    public void Adicionar(Usuario usuario) => db.Usuarios.Add(usuario);
    public Task BloquearSetorAsync(Guid setorId, CancellationToken cancellationToken = default)
    {
        if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("A configuração do setor exige uma transação de comando.");
        var trava = $"configuracao-setor:{db.EmpresaId}:{setorId}";
        return db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({trava}, 0))", cancellationToken);
    }
    public Task BloquearVinculosUsuarioAsync(Guid usuarioId, CancellationToken cancellationToken = default)
    {
        if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("A validação de vínculos exige uma transação de comando.");
        var trava = $"vinculos:{db.EmpresaId}:{usuarioId}";
        return db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({trava}, 0))", cancellationToken);
    }
    public Task<Usuario?> ObterUsuarioAsync(Guid id, CancellationToken cancellationToken = default) =>
        db.Usuarios.AsTracking().SingleOrDefaultAsync(u => u.Id == id, cancellationToken);
    public Task<VinculoSetor?> ObterVinculoAsync(Guid id, CancellationToken cancellationToken = default) =>
        db.Vinculos.AsTracking().SingleOrDefaultAsync(v => v.Id == id, cancellationToken);
    public Task<Empresa?> ObterEmpresaAsync(CancellationToken cancellationToken = default) =>
        db.Empresas.AsTracking().SingleOrDefaultAsync(cancellationToken);

    public async Task<Setor?> ObterSetorAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (db.ChangeTracker.Entries().Any(e => e.State != EntityState.Unchanged))
            throw new InvalidOperationException("Carregue a configuração antes de iniciar alterações na unidade de trabalho.");
        var existente = db.Setores.Local.SingleOrDefault(s => s.Id == id);
        if (existente is not null)
        {
            if (!db.Entry(existente).Collection(x => x.Categorias).IsLoaded || !db.Entry(existente).Collection(x => x.Filas).IsLoaded)
                throw new InvalidOperationException("O setor já rastreado não tem seu agregado completamente carregado.");
            return existente;
        }
        IQueryable<Setor> Completa() => db.Setores.AsTracking().Include(x => x.Sla)
            .Include(x => x.Categorias).Include(x => x.Filas).ThenInclude(x => x.Membros);
        if (db.Database.CurrentTransaction is not null)
            return await Completa().AsSingleQuery().SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        // Preserva a empresa, quando já foi lida pelo chamador; descarta só o grafo desta tentativa.
        var anteriores = db.ChangeTracker.Entries().Select(e => e.Entity).ToHashSet(ReferenceEqualityComparer.Instance);
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            foreach (var entrada in db.ChangeTracker.Entries().Where(e => !anteriores.Contains(e.Entity)).ToArray())
                entrada.State = EntityState.Detached;
            await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead, cancellationToken);
            var setor = await Completa().AsSplitQuery().SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
            await tx.CommitAsync(cancellationToken);
            return setor;
        });
    }
}
