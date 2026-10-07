using Microsoft.EntityFrameworkCore;
using v8desk.application.Abstractions;
using v8desk.domain.Entities;
using v8desk.infrastructure.Persistence;

namespace v8desk.infrastructure.Repositories;

public sealed class ChamadoRepository(V8DeskDbContext db) : IChamadoRepository
{
    public void Adicionar(Chamado chamado) => db.Chamados.Add(chamado);

    // O agregado retornado é completo. Consultas de tela usam projeções separadas.
    public async Task<Chamado?> ObterParaAtualizacaoAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (db.ChangeTracker.Entries().Any())
            throw new InvalidOperationException("Carregue o chamado em uma unidade de trabalho nova, antes de outros registros.");
        if (db.Database.CurrentTransaction is not null)
        {
            var agregado = await ConsultaCompleta().AsSplitQuery().SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
            // Em ReadCommitted, cada comando do agregado altera sua versão na mesma transação.
            // A segunda leitura detecta mudanças durante os SELECTs sem multiplicar o grafo por JOINs.
            var versao = await db.Chamados.Where(x => x.Id == id).Select(x => (long?)x.Versao)
                .SingleOrDefaultAsync(cancellationToken);
            if (agregado is not null && versao != agregado.Versao)
                throw new v8desk.domain.Exceptions.RegraNegocioException("O chamado mudou durante a leitura. Atualize a tela e tente novamente.");
            return agregado;
        }
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            // Uma tentativa anterior pode ter materializado parte do grafo.
            db.ChangeTracker.Clear();
            await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead, cancellationToken);
            var chamado = await ConsultaCompleta().AsSplitQuery().SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
            await tx.CommitAsync(cancellationToken);
            return chamado;
        });
    }

    private IQueryable<Chamado> ConsultaCompleta() => db.Chamados.AsTracking()
                .Include(x => x.Eventos.OrderBy(e => e.Sequencia))
                .Include(x => x.Mensagens.OrderBy(e => e.CriadaEm).ThenBy(e => e.Id))
                .Include(x => x.Ciclos.OrderBy(c => c.Numero)).ThenInclude(c => c.Periodos.OrderBy(p => p.Entrada))
                .Include(x => x.Ciclos.OrderBy(c => c.Numero)).ThenInclude(c => c.CiclosSla.OrderBy(s => s.IniciadoEm));
}
