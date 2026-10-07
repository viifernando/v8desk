using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using v8desk.api.Errors;
using v8desk.api.Resilience;
using v8desk.api.Security;
using v8desk.api.Tenancy;
using v8desk.api.Integracoes;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 128 * 1024);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<v8desk.application.Abstractions.IEmpresaAtual, EmpresaAtualHttp>();
builder.Services.AddScoped<v8desk.application.Abstractions.IUsuarioAtual, EmpresaAtualHttp>();
builder.Services.AddScoped<v8desk.application.Abstractions.IAutorizacaoEmpresa, AutorizacaoEmpresaHttp>();
builder.Services.AdicionarAutenticacaoApi(builder.Configuration, builder.Environment.IsDevelopment());
builder.Services.AdicionarLimitesApi(builder.Configuration);
var proxyConfigurado = builder.Services.AdicionarProxyConfiavel(builder.Configuration);
var conexao = builder.Configuration.GetConnectionString("V8Desk");
var persistenciaConfigurada = !string.IsNullOrWhiteSpace(conexao);
if (!persistenciaConfigurada && builder.Configuration.GetValue("Authentication:EntraEnabled", false))
    throw new InvalidOperationException("Configure ConnectionStrings:V8Desk para habilitar a autenticação Microsoft.");
if (persistenciaConfigurada)
{
    v8desk.application.DependencyInjection.AdicionarAplicacao(builder.Services);
    v8desk.infrastructure.DependencyInjection.AdicionarInfraestrutura(builder.Services, conexao!);
    builder.Services.AdicionarIntegracoesApi(builder.Configuration, builder.Environment.IsDevelopment());
}
else if (!builder.Environment.IsDevelopment())
    throw new InvalidOperationException("Configure ConnectionStrings:V8Desk antes de iniciar o servidor.");

builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false));
    options.JsonSerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow;
    options.JsonSerializerOptions.MaxDepth = 32;
});
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
{
    var problema = context.ProblemDetails;
    if (!problema.Extensions.ContainsKey("code"))
    {
        var (titulo, detalhe, codigo) = ProblemasApi.ParaStatus(problema.Status ?? context.HttpContext.Response.StatusCode);
        problema.Title = titulo;
        problema.Detail = detalhe;
        problema.Extensions["code"] = codigo;
    }
    problema.Extensions["traceId"] = System.Diagnostics.Activity.Current?.Id ?? context.HttpContext.TraceIdentifier;
    problema.Instance = context.HttpContext.Request.Path;
});
builder.Services.Configure<Microsoft.AspNetCore.Mvc.ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = ValidacaoHttp.Responder;
});
builder.Services.AddHealthChecks().AddCheck<ProntidaoHealthCheck>("prontidao");
builder.Services.AddOpenApi();

var app = builder.Build();
if (proxyConfigurado) app.UseForwardedHeaders();
app.UseExceptionHandler();
app.UseStatusCodePages(async context =>
{
    var response = context.HttpContext.Response;
    var (titulo, detalhe, codigo) = ProblemasApi.ParaStatus(response.StatusCode);
    await ProblemasApi.EscreverAsync(context.HttpContext, response.StatusCode, titulo, detalhe, codigo,
        context.HttpContext.RequestAborted);
});
if (!app.Environment.IsDevelopment()) app.UseHsts();
app.UseHttpsRedirection();
app.UseRouting();
app.UseRequestTimeouts();
app.Use(async (context, next) =>
{
    // Prontuários e respostas nunca devem ser armazenados em cache compartilhado.
    context.Response.Headers.CacheControl = "no-store";
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Request-Id"] = context.TraceIdentifier;
    await next(context);
});
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();
app.Use(async (context, next) =>
{
    if (context.Request.ContentLength > 128 * 1024)
    {
        await ProblemasApi.EscreverAsync(context, 413, "Solicitação muito grande",
            "Reduza o tamanho dos dados enviados e tente novamente.", "requisicao_grande", context.RequestAborted);
        return;
    }
    if (!persistenciaConfigurada && context.GetEndpoint()?.Metadata.GetMetadata<Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor>() is not null)
    {
        await ProblemasApi.EscreverAsync(context, 503, "Serviço temporariamente indisponível",
            "O serviço ainda não está disponível. Tente novamente em alguns instantes.", "servico_indisponivel", context.RequestAborted);
        return;
    }
    await next(context);
});
if (app.Environment.IsDevelopment()) app.MapOpenApi().AllowAnonymous();
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false, ResponseWriter = EscreverSaude
}).AllowAnonymous();
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    ResponseWriter = EscreverSaude
}).AllowAnonymous();
app.MapControllers();
app.Run();

static Task EscreverSaude(HttpContext context, HealthReport report)
{
    if (report.Status != HealthStatus.Healthy)
        return ProblemasApi.EscreverAsync(context, 503, "Serviço temporariamente indisponível",
            "O serviço ainda não está pronto para atender.", "servico_indisponivel", context.RequestAborted);
    return context.Response.WriteAsJsonAsync(new { status = "disponivel" }, context.RequestAborted);
}

public partial class Program { }
