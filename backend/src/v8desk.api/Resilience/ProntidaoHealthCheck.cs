using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace v8desk.api.Resilience;

public sealed class ProntidaoHealthCheck(IConfiguration config) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        var conexao = config.GetConnectionString("V8Desk");
        if (string.IsNullOrWhiteSpace(conexao) || !config.GetValue("Authentication:EntraEnabled", false) &&
            (string.IsNullOrWhiteSpace(config["Authentication:Authority"]) || string.IsNullOrWhiteSpace(config["Authentication:Audience"])))
            return HealthCheckResult.Unhealthy();
        try
        {
            using var prazo = CancellationTokenSource.CreateLinkedTokenSource(ct);
            prazo.CancelAfter(TimeSpan.FromSeconds(3));
            await using var db = new NpgsqlConnection(conexao);
            await db.OpenAsync(prazo.Token);
            await using var comando = new NpgsqlCommand("SELECT 1", db);
            await comando.ExecuteScalarAsync(prazo.Token);
            return HealthCheckResult.Healthy();
        }
        catch (Exception) when (!ct.IsCancellationRequested) { return HealthCheckResult.Unhealthy(); }
    }
}
