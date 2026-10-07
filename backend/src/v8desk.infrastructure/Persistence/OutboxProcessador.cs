using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using v8desk.application.Abstractions;
using v8desk.domain.Exceptions;

namespace v8desk.infrastructure.Persistence;

// Invocado pelo host com escopo de empresa e publicador configurado. Não descarta mensagens.
public sealed class OutboxProcessador(V8DeskDbContext db, IPublicadorOutbox publicador, ILogger<OutboxProcessador> logger)
{
    public async Task<int> ProcessarAsync(int tamanho = 20, CancellationToken cancellationToken = default)
    {
        if (tamanho is < 1 or > 100) throw new ValidacaoDominioException("lote_invalido", "tamanho", "Use lotes de 1 a 100 mensagens.");
        if (db.ChangeTracker.Entries().Any() || db.Database.CurrentTransaction is not null)
            throw new InvalidOperationException("Processe a outbox em uma unidade de trabalho nova.");
        var total = 0;
        for (var i = 0; i < tamanho; i++)
        {
            var resultado = await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
            {
                db.ChangeTracker.Clear();
                await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
                var agora = DateTimeOffset.UtcNow;
                // Uma transação por mensagem limita o tempo de retenção de locks.
                var mensagens = await db.Set<OutboxMensagem>().FromSqlInterpolated($"SELECT outbox.*, xmin FROM outbox WHERE empresa_id = {db.EmpresaId} AND processada_em IS NULL AND disponivel_em <= {agora} ORDER BY disponivel_em, criada_em, id LIMIT 1 FOR UPDATE SKIP LOCKED")
                    .AsTracking().ToListAsync(cancellationToken);
                var publicadas = 0;
                foreach (var mensagem in mensagens)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    using var limite = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    limite.CancelAfter(TimeSpan.FromSeconds(30));
                    try
                    {
                        await publicador.PublicarAsync(new(mensagem.Id, mensagem.EmpresaId, mensagem.Tipo, mensagem.Payload), limite.Token);
                        mensagem.Confirmar(DateTimeOffset.UtcNow);
                        publicadas++;
                    }
                    catch (Exception erro) when (!cancellationToken.IsCancellationRequested)
                    {
                        logger.LogError(erro, "Falha na outbox {EmpresaId}/{MensagemId}", mensagem.EmpresaId, mensagem.Id);
                        mensagem.RegistrarFalha(DateTimeOffset.UtcNow);
                    }
                }
                await db.SaveChangesAsync(cancellationToken);
                await tx.CommitAsync(cancellationToken);
                return (Encontrada: mensagens.Count != 0, Publicadas: publicadas);
            });
            if (!resultado.Encontrada) break;
            total += resultado.Publicadas;
        }
        return total;
    }
}
