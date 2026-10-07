using Microsoft.EntityFrameworkCore;
using v8desk.application.Abstractions;
using v8desk.domain.Entities;
using v8desk.infrastructure.Persistence;

namespace v8desk.infrastructure.Repositories;

public sealed class ConfiguracaoRepository(V8DeskDbContext db) : IConfiguracaoRepository
{
    public void Adicionar(Setor setor) => db.Setores.Add(setor);
    public Task<Empresa?> ObterEmpresaAsync(CancellationToken cancellationToken = default) =>
        db.Empresas.AsTracking().SingleOrDefaultAsync(cancellationToken);

    public async Task<Setor?> ObterSetorAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (db.ChangeTracker.Entries().Any(e => e.State != EntityState.Unchanged))
            throw new InvalidOperationException("Carregue a configuração antes de iniciar alterações na unidade de trabalho.");
        if (db.ChangeTracker.Entries<Setor>().Any())
            throw new InvalidOperationException("Carregue a configuração do setor uma vez por unidade de trabalho.");
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
