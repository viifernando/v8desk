using Microsoft.EntityFrameworkCore;
using Npgsql;
using v8desk.application.Abstractions;
using v8desk.application.Integracoes;
using v8desk.infrastructure.Integracoes;
using v8desk.infrastructure.Persistence;
using v8desk.infrastructure.Repositories;

namespace v8desk.api.Integracoes;

public sealed class EmailWorker(NpgsqlDataSource source, IServiceScopeFactory scopes, IConfiguration config,
    ILogger<EmailWorker> logger, TimeProvider relogio) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var cursor = Guid.Empty;
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await using var comando = source.CreateCommand("SELECT empresa_id FROM integracoes_empresa WHERE empresa_id > @cursor ORDER BY empresa_id LIMIT 8");
                comando.Parameters.AddWithValue("cursor", cursor);
                var empresas = new List<Guid>();
                await using (var r = await comando.ExecuteReaderAsync(ct))
                    while (await r.ReadAsync(ct)) empresas.Add(r.GetGuid(0));
                cursor = empresas.Count == 0 ? Guid.Empty : empresas[^1];
                await Parallel.ForEachAsync(empresas, new ParallelOptions { MaxDegreeOfParallelism = 4, CancellationToken = ct },
                    async (empresa, token) =>
                    {
                        try
                        {
                            using var scope = scopes.CreateScope();
                            var opcoes = new DbContextOptionsBuilder<V8DeskDbContext>().UseNpgsql(config.GetConnectionString("V8Desk"),
                                pg => pg.CommandTimeout(15)).Options;
                            await using (var db = new V8DeskDbContext(opcoes, new EmpresaWorker(empresa)))
                                await new OutboxProcessador(db, new PublicadorEmailOutbox(db),
                                    scope.ServiceProvider.GetRequiredService<ILogger<OutboxProcessador>>()).ProcessarAsync(20, token);
                            await using (var db = new V8DeskDbContext(opcoes, new EmpresaWorker(empresa)))
                                await new ProcessadorEmail(db, new IntegracoesRepository(db), new AcessoRepository(db),
                                    scope.ServiceProvider.GetRequiredService<ITransporteEmail>(), relogio).ProcessarAsync(10, token);
                        }
                        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
                        catch (Exception) { logger.LogWarning("Não foi possível processar notificações da empresa {EmpresaId}. Nova tentativa no próximo ciclo.", empresa); }
                    });
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (Exception) { logger.LogWarning("Não foi possível consultar a fila de notificações. Nova tentativa no próximo ciclo."); }
            try { if (!await timer.WaitForNextTickAsync(ct)) break; }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
        }
    }
    private sealed record EmpresaWorker(Guid EmpresaId) : IEmpresaAtual;
}
