using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Http.Timeouts;
using v8desk.api.Errors;

namespace v8desk.api.Resilience;

public static class LimitesApi
{
    public static void AdicionarLimitesApi(this IServiceCollection services, IConfiguration config)
    {
        var requisicoes = config.GetValue("Api:RequisicoesPorMinuto", 120);
        var concorrentes = config.GetValue("Api:RequisicoesConcorrentes", 64);
        var prazo = config.GetValue("Api:PrazoRequisicaoSegundos", 60);
        if (requisicoes is < 1 or > 100000 || concorrentes is < 1 or > 10000)
            throw new InvalidOperationException("Configure limites positivos de requisições na seção Api.");
        if (prazo is < 1 or > 600) throw new InvalidOperationException("Configure Api:PrazoRequisicaoSegundos entre 1 e 600.");
        services.AddRequestTimeouts(options => options.DefaultPolicy = new RequestTimeoutPolicy
        {
            Timeout = TimeSpan.FromSeconds(prazo), TimeoutStatusCode = 503,
            WriteTimeoutResponse = context =>
            {
                context.Response.Headers.RetryAfter = "5";
                return ProblemasApi.EscreverAsync(context, 503, "Serviço temporariamente indisponível",
                    "O serviço demorou para responder. Tente novamente usando a mesma chave de operação.",
                    "prazo_requisicao_excedido");
            }
        });
        services.AddRateLimiter(options =>
        {
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(_ =>
                RateLimitPartition.GetConcurrencyLimiter("servidor", _ => new ConcurrencyLimiterOptions
                {
                    PermitLimit = concorrentes, QueueLimit = 0, QueueProcessingOrder = QueueProcessingOrder.OldestFirst
                }));
            options.AddPolicy("usuario", context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    context.User.Identity?.IsAuthenticated == true
                        ? context.User.FindFirst("empresa_id")?.Value + ":" + (context.User.FindFirst("sub")?.Value
                            ?? context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value)
                        : "anonimo:" + context.Connection.RemoteIpAddress,
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = requisicoes, Window = TimeSpan.FromMinutes(1), QueueLimit = 0,
                        AutoReplenishment = true
                    }));
            options.OnRejected = async (context, ct) =>
            {
                var segundos = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var espera)
                    ? Math.Max(1, (int)Math.Ceiling(espera.TotalSeconds)) : 1;
                context.HttpContext.Response.Headers.RetryAfter = segundos.ToString(CultureInfo.InvariantCulture);
                await ProblemasApi.EscreverAsync(context.HttpContext, 429, "Aguarde um momento",
                    "O serviço está recebendo muitas solicitações. Aguarde antes de tentar novamente.", "limite_requisicoes", ct);
            };
        });
    }
}
