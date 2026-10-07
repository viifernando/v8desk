using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using v8desk.api.Errors;

namespace v8desk.api.Security;

public static class Autenticacao
{
    public static bool AdicionarAutenticacaoApi(this IServiceCollection services, IConfiguration config, bool desenvolvimento)
    {
        var authority = config["Authentication:Authority"];
        var audience = config["Authentication:Audience"];
        var configurada = !string.IsNullOrWhiteSpace(authority) && !string.IsNullOrWhiteSpace(audience);
        if ((!string.IsNullOrWhiteSpace(authority) || !string.IsNullOrWhiteSpace(audience)) && !configurada)
            throw new InvalidOperationException("Configure Authentication:Authority e Authentication:Audience juntos.");
        if (configurada && (!Uri.TryCreate(authority, UriKind.Absolute, out var uri) || uri.Scheme != "https"))
            throw new InvalidOperationException("Authentication:Authority deve ser um endereço HTTPS.");
        if (!configurada && !desenvolvimento)
            throw new InvalidOperationException("Configure o provedor JWT em Authentication:Authority e Authentication:Audience.");
        if (configurada)
            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
            {
                options.Authority = authority;
                options.Audience = audience;
                options.RequireHttpsMetadata = true;
                options.MapInboundClaims = false;
                options.IncludeErrorDetails = false;
                options.TokenValidationParameters.NameClaimType = "sub";
                options.TokenValidationParameters.RoleClaimType = "role";
                options.TokenValidationParameters.ValidateIssuer = true;
                options.TokenValidationParameters.ValidateAudience = true;
                options.TokenValidationParameters.ValidateLifetime = true;
                options.TokenValidationParameters.ValidateIssuerSigningKey = true;
                options.TokenValidationParameters.ClockSkew = TimeSpan.FromSeconds(30);
                options.Events = new JwtBearerEvents
                {
                    OnChallenge = async context =>
                    {
                        context.HandleResponse();
                        context.Response.Headers.WWWAuthenticate = "Bearer";
                        await ProblemasApi.EscreverAsync(context.HttpContext, 401, "Entre para continuar",
                            "Sua sessão não está válida. Entre novamente para continuar.", "autenticacao_necessaria", context.HttpContext.RequestAborted);
                    },
                    OnForbidden = context => ProblemasApi.EscreverAsync(context.HttpContext, 403, "Ação não permitida",
                        "Sua conta não tem permissão para esta ação.", "acesso_negado", context.HttpContext.RequestAborted)
                };
            });
        else
            services.AddAuthentication("sem-provedor").AddScheme<AuthenticationSchemeOptions, SemProvedorHandler>("sem-provedor", _ => { });
        services.AddAuthorization(options =>
        {
            var contaEmpresa = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser().RequireAssertion(context => IdentidadeValida(context.User)).Build();
            options.AddPolicy("conta_empresa", contaEmpresa);
            options.FallbackPolicy = contaEmpresa;
        });
        return configurada;
    }

    public static bool IdentidadeValida(ClaimsPrincipal usuario) =>
        Guid.TryParse(usuario.FindFirst("sub")?.Value ?? usuario.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id)
        && id != Guid.Empty && Guid.TryParse(usuario.FindFirst("empresa_id")?.Value, out var empresa) && empresa != Guid.Empty;
}

// Sem provedor em desenvolvimento: os endpoints permanecem protegidos.
public sealed class SemProvedorHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync() => Task.FromResult(AuthenticateResult.NoResult());
    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.Headers.WWWAuthenticate = "Bearer";
        return ProblemasApi.EscreverAsync(Context, 401, "Entre para continuar",
            "O acesso exige uma sessão válida. Configure o provedor de autenticação para entrar.", "autenticacao_necessaria", Context.RequestAborted);
    }
}
