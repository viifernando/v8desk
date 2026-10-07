using Microsoft.EntityFrameworkCore;
using v8desk.application.Abstractions;
using v8desk.domain.Entities;
using v8desk.infrastructure.Persistence;

namespace v8desk.infrastructure.Repositories;

public sealed class ChamadoRepository(V8DeskDbContext db) : IChamadoRepository
{
    public void Adicionar(Chamado chamado) => db.Chamados.Add(chamado);

    // Carrega somente estado necessário ao comando; o prontuário é consultado separadamente.
    public Task<Chamado?> ObterParaAtualizacaoAsync(Guid id, CancellationToken cancellationToken = default, Guid? mensagemId = null)
        => db.Chamados.AsTracking().AsSplitQuery()
            .Include(x => x.Mensagens.Where(e => mensagemId != null && e.Id == mensagemId))
            .Include(x => x.Ciclos.OrderByDescending(c => c.Numero).Take(1)).ThenInclude(c => c.Periodos.Where(p => p.Saida == null))
            .Include(x => x.Ciclos.OrderByDescending(c => c.Numero).Take(1)).ThenInclude(c => c.CiclosSla.Where(s => s.FinalizadoEm == null))
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
}
