using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using v8desk.application.Integracoes;
using v8desk.api.Errors;

namespace v8desk.api.Security;

public interface IMetadadosEntra { BaseConfigurationManager Obter(Guid tenantId); }
public sealed class MetadadosEntra : IMetadadosEntra
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<Guid, BaseConfigurationManager> _cache = new();
    public BaseConfigurationManager Obter(Guid tenantId) => _cache.GetOrAdd(tenantId, id =>
        new ConfigurationManager<OpenIdConnectConfiguration>(
            "https://login.microsoftonline.com/" + id + "/v2.0/.well-known/openid-configuration",
            new OpenIdConnectConfigurationRetriever(), new HttpDocumentRetriever { RequireHttps = true }));
}
public sealed class MicrosoftEntraHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger,
    UrlEncoder encoder, IIdentidadeMicrosoftRepository identidades, IMetadadosEntra metadados)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var header = Request.Headers.Authorization.ToString();
        if (!header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) return AuthenticateResult.NoResult();
        var texto = header[7..].Trim();
        if (texto.Length is 0 or > 16384) return AuthenticateResult.Fail("Sessão Microsoft inválida.");
        var handler = new JsonWebTokenHandler { MapInboundClaims = false };
        try
        {
            // Claims não validadas servem somente para buscar uma associação conhecida.
            var preliminar = handler.ReadJsonWebToken(texto);
            if (!Guid.TryParse(ValorUnico(preliminar.Claims, "tid"), out var tenant) || tenant == Guid.Empty ||
                !Guid.TryParse(ValorUnico(preliminar.Claims, "oid"), out var objeto) || objeto == Guid.Empty ||
                !Guid.TryParse(ValorUnico(preliminar.Claims, "aud"), out var api) || api == Guid.Empty)
                return AuthenticateResult.Fail("Sessão Microsoft inválida.");
            var candidata = await identidades.ResolverAsync(tenant, api, objeto, Context.RequestAborted);
            if (candidata?.Ativa != true) return AuthenticateResult.Fail("Conta Microsoft não habilitada.");
            var validacao = await handler.ValidateTokenAsync(texto, new TokenValidationParameters
            {
                ConfigurationManager = metadados.Obter(tenant),
                ValidIssuer = "https://login.microsoftonline.com/" + tenant + "/v2.0",
                ValidAudience = api.ToString(),
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                RequireSignedTokens = true,
                RequireExpirationTime = true,
                ClockSkew = TimeSpan.FromSeconds(30),
                ValidAlgorithms = [SecurityAlgorithms.RsaSha256]
            });
            if (!validacao.IsValid) return AuthenticateResult.Fail("Sessão Microsoft inválida.");
            var claims = validacao.ClaimsIdentity.Claims.ToArray();
            if (ValorUnico(claims, "tid") != tenant.ToString() || ValorUnico(claims, "oid") != objeto.ToString()
                || ValorUnico(claims, "ver") != "2.0"
                || !(ValorUnico(claims, "scp")?.Split(' ').Contains("access_as_user") ?? false))
                return AuthenticateResult.Fail("O token não autoriza acesso à API.");
            // Revogação e desativação são verificadas novamente após a validação criptográfica.
            var vinculo = await identidades.ResolverAsync(tenant, api, objeto, Context.RequestAborted);
            if (vinculo?.Ativa != true || vinculo.EmpresaId != candidata.EmpresaId || vinculo.UsuarioId != candidata.UsuarioId)
                return AuthenticateResult.Fail("Conta Microsoft não habilitada.");
            var acesso = await identidades.AcessoAsync(vinculo.EmpresaId, Context.RequestAborted);
            if (acesso is null || acesso.TenantId != tenant || acesso.ApiClienteId != api ||
                ValorUnico(claims, "azp") != acesso.ClienteLoginId.ToString())
                return AuthenticateResult.Fail("O aplicativo de login não está autorizado.");
            var internos = new List<Claim>
            {
                new("sub", vinculo.UsuarioId.ToString()), new("empresa_id", vinculo.EmpresaId.ToString()),
                new("tid", tenant.ToString()), new("oid", objeto.ToString()), new("provedor", "microsoft")
            };
            if (vinculo.Administrador) internos.Add(new("role", "administrador_empresa"));
            var principal = new ClaimsPrincipal(new ClaimsIdentity(internos, Scheme.Name, "sub", "role"));
            return AuthenticateResult.Success(new(principal, Scheme.Name));
        }
        catch (Exception e) when (e is SecurityTokenException or ArgumentException or System.Text.Json.JsonException)
        { return AuthenticateResult.Fail("Sessão Microsoft inválida."); }
    }
    private static string? ValorUnico(IEnumerable<Claim> claims, string tipo)
    {
        var valores = claims.Where(c => c.Type == tipo).Take(2).ToArray();
        return valores.Length == 1 ? valores[0].Value : null;
    }
    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.Headers.WWWAuthenticate = "Bearer";
        return ProblemasApi.EscreverAsync(Context, 401, "Entre para continuar",
            "Sua sessão Microsoft não está válida ou sua conta não está habilitada. Entre novamente ou fale com o administrador.",
            "autenticacao_necessaria", Context.RequestAborted);
    }
    protected override Task HandleForbiddenAsync(AuthenticationProperties properties) =>
        ProblemasApi.EscreverAsync(Context, 403, "Ação não permitida", "Sua conta não tem permissão para esta ação.", "acesso_negado", Context.RequestAborted);
}
