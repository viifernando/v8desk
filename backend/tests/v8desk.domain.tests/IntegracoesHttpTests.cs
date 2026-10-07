using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using v8desk.api.Controllers;
using v8desk.api.Security;
using v8desk.application.Abstractions;
using v8desk.application.Integracoes;

namespace v8desk.domain.tests;

public static class IntegracoesHttpTests
{
    public static void ConfiguracaoHttpExigeAdminEMascaraSegredos() => ConfigurarAsync().GetAwaiter().GetResult();
    public static void EntraValidaTokenMapeiaUsuarioEEvitaEscalada() => EntraAsync().GetAwaiter().GetResult();
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
    private static async Task ConfigurarAsync()
    {
        using var host = new Host();
        using var client = host.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        client.DefaultRequestHeaders.Add("X-Usuario-Teste", host.F.Cenario.Autor.Id.ToString());
        client.DefaultRequestHeaders.Add("X-Empresa-Teste", host.F.Cenario.Empresa.Id.ToString());
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/integracoes")).StatusCode);
        client.DefaultRequestHeaders.Add("X-Admin-Teste", "sim");
        client.DefaultRequestHeaders.Add("Idempotency-Key", "smtp-http");
        var config = new
        {
            provedor = "Smtp",
            remetente = "suporte@empresa.test",
            nomeRemetente = "V8Desk",
            hostSmtp = "smtp.empresa.test",
            usuarioSmtp = "usuario",
            segredo = "credencial-secreta",
            segurancaSmtp = "StartTls",
            portaSmtp = 587
        };
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/v1/integracoes/email", config)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/v1/integracoes/email", config)).StatusCode);
        Assert.Equal(1L, host.F.Config!.RevisaoEmail);
        var consulta = await client.GetAsync("/api/v1/integracoes");
        var texto = await consulta.Content.ReadAsStringAsync();
        Assert.False(texto.Contains("credencial-secreta")); Assert.False(texto.Contains("credencialProtegida"));
        using var resumo = JsonDocument.Parse(texto);
        Assert.True(resumo.RootElement.GetProperty("email").GetProperty("temCredencial").GetBoolean());
        client.DefaultRequestHeaders.Remove("Idempotency-Key"); client.DefaultRequestHeaders.Add("Idempotency-Key", "ativar-http");
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/v1/integracoes/email/ativacao", new { ativo = true })).StatusCode);
        client.DefaultRequestHeaders.Remove("Idempotency-Key"); client.DefaultRequestHeaders.Add("Idempotency-Key", "contato-http");
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/v1/integracoes/usuarios/" + host.F.Cenario.Autor.Id + "/email",
            new { email = "admin@empresa.test" })).StatusCode);
        client.DefaultRequestHeaders.Remove("Idempotency-Key"); client.DefaultRequestHeaders.Add("Idempotency-Key", "teste-http");
        Assert.Equal(HttpStatusCode.Accepted, (await client.PostAsync("/api/v1/integracoes/email/testar", null)).StatusCode);
        Assert.Equal(1, host.F.Testes.Count);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync("/api/v1/integracoes/email/diagnostico", null)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/v1/integracoes/email/entregas?tamanho=101")).StatusCode);
        client.DefaultRequestHeaders.Remove("Idempotency-Key"); client.DefaultRequestHeaders.Add("Idempotency-Key", "outra-empresa");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/v1/integracoes/usuarios/" + Guid.NewGuid() + "/email",
            new { email = "outro@empresa.test" })).StatusCode);
    }
    private static async Task EntraAsync()
    {
        using var host = new Host();
        host.F.Config = new(host.F.Cenario.Empresa.Id);
        var tenant = Guid.NewGuid(); var api = Guid.NewGuid(); var login = Guid.NewGuid();
        host.F.Config.ConfigurarMicrosoft(new(tenant, api, login)); host.F.Config.AtivarMicrosoft(true);
        using var client = host.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/acesso/microsoft/" + host.F.Cenario.Empresa.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/v1/acesso/microsoft/" + Guid.NewGuid())).StatusCode);
        string Token(Guid? tid = null, Guid? aud = null, Guid? oid = null, string? scope = "access_as_user", Guid? azp = null,
            bool expirado = false, SecurityKey? key = null, string? issuer = null)
        {
            var t = tid ?? tenant;
            var claims = new List<Claim> { new("tid", t.ToString()), new("oid", (oid ?? host.F.Objeto).ToString()),
                new("sub", "identificador-externo-sem-relacao-com-guid-interno"), new("ver", "2.0"), new("azp", (azp ?? login).ToString()),
                new("role", "administrador_empresa"), new("empresa_id", Guid.NewGuid().ToString()) };
            if (scope is not null) claims.Add(new("scp", scope));
            return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
                issuer ?? "https://login.microsoftonline.com/" + t + "/v2.0", (aud ?? api).ToString(), claims,
                DateTime.UtcNow.AddHours(-1), expirado ? DateTime.UtcNow.AddMinutes(-5) : DateTime.UtcNow.AddMinutes(5),
                new SigningCredentials(key ?? host.Chave, SecurityAlgorithms.RsaSha256)));
        }
        host.F.AdministradorMicrosoft = false;
        client.DefaultRequestHeaders.Authorization = new("Bearer", Token());
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/integracoes")).StatusCode); // role externo ignorado
        host.F.AdministradorMicrosoft = true;
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/integracoes")).StatusCode);
        using var outroRsa = RSA.Create(2048);
        foreach (var token in new[] { Token(tid: Guid.NewGuid()), Token(aud: Guid.NewGuid()), Token(oid: Guid.NewGuid()),
            Token(scope: null), Token(scope: "User.Read"), Token(azp: Guid.NewGuid()), Token(expirado: true),
            Token(key: new RsaSecurityKey(outroRsa)), Token(issuer: "https://login.microsoftonline.com/" + Guid.NewGuid() + "/v2.0") })
        {
            client.DefaultRequestHeaders.Authorization = new("Bearer", token);
            var rejeitada = await client.GetAsync("/api/v1/integracoes");
            Assert.Equal(HttpStatusCode.Unauthorized, rejeitada.StatusCode);
            Assert.False((await rejeitada.Content.ReadAsStringAsync()).Contains("IDX"));
        }
        client.DefaultRequestHeaders.Authorization = new("Bearer", Token());
        host.F.Config.AtivarMicrosoft(false);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/integracoes")).StatusCode);
        host.F.Config.AtivarMicrosoft(true); host.F.Cenario.Autor.Desativar();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/integracoes")).StatusCode);
    }
    private sealed class Host : WebApplicationFactory<IntegracoesController>
    {
        internal readonly IntegracoesTests.Fixture F = new();
        private readonly RSA _rsa = RSA.Create(2048);
        internal RsaSecurityKey Chave => new(_rsa) { KeyId = "entra-teste" };
        protected override void Dispose(bool disposing) { base.Dispose(disposing); if (disposing) _rsa.Dispose(); }
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseContentRoot(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/v8desk.api")));
            builder.UseEnvironment("Development");
            builder.UseSetting("ConnectionStrings:V8Desk", "Host=localhost;Database=v8desk_test;Username=teste;Password=teste");
            builder.UseSetting("Authentication:Authority", ""); builder.UseSetting("Authentication:Audience", "");
            builder.UseSetting("Authentication:EntraEnabled", "true");
            builder.UseSetting("Integrations:WorkerEnabled", "false");
            builder.ConfigureTestServices(services =>
            {
                services.AddLogging(o => o.ClearProviders());
                services.AddAuthentication(options => { options.DefaultAuthenticateScheme = "integracao-teste"; options.DefaultChallengeScheme = "integracao-teste"; options.DefaultForbidScheme = "integracao-teste"; })
                    .AddScheme<AuthenticationSchemeOptions, ApiHttpTests.IdentidadeTeste>("teste", _ => { })
                    .AddPolicyScheme("integracao-teste", "Teste", options =>
                        options.ForwardDefaultSelector = context => context.Request.Headers.Authorization.ToString().StartsWith("Bearer ") ? "v8desk" : "teste");
                services.RemoveAll<IMetadadosEntra>(); services.AddSingleton<IMetadadosEntra>(new Metadados(this));
                services.RemoveAll<IIdentidadeMicrosoftRepository>(); services.AddSingleton<IIdentidadeMicrosoftRepository>(F);
                services.RemoveAll<IIntegracoesRepository>(); services.AddSingleton<IIntegracoesRepository>(F);
                services.RemoveAll<IProtecaoCredencial>(); services.AddSingleton(F.Protecao);
                services.RemoveAll<ITransporteEmail>(); services.AddSingleton(F.Transporte);
                services.RemoveAll<IDataProtectionProvider>(); services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
                services.RemoveAll<IAcessoRepository>(); services.AddSingleton<IAcessoRepository>(F.Cenario);
                services.RemoveAll<IConfiguracaoRepository>(); services.AddSingleton<IConfiguracaoRepository>(F.Cenario);
                services.RemoveAll<IChamadoRepository>(); services.AddSingleton<IChamadoRepository>(F.Cenario);
                services.RemoveAll<IExecutorComandoIdempotente>(); services.AddSingleton<IExecutorComandoIdempotente>(F.Cenario);
                services.RemoveAll<IUnidadeTrabalho>(); services.AddSingleton<IUnidadeTrabalho>(F.Cenario);
            });
        }
    }
    private sealed class Metadados(Host host) : IMetadadosEntra
    {
        public BaseConfigurationManager Obter(Guid tenant) => new ConfigurationManager<OpenIdConnectConfiguration>(
            "https://login.microsoftonline.com/" + tenant + "/v2.0/.well-known/openid-configuration", new Recuperador(host, tenant), new HttpDocumentRetriever());
        private sealed class Recuperador(Host host, Guid tenant) : IConfigurationRetriever<OpenIdConnectConfiguration>
        {
            public Task<OpenIdConnectConfiguration> GetConfigurationAsync(string address, IDocumentRetriever retriever, CancellationToken ct)
            {
                var c = new OpenIdConnectConfiguration { Issuer = "https://login.microsoftonline.com/" + tenant + "/v2.0" };
                c.SigningKeys.Add(host.Chave); return Task.FromResult(c);
            }
        }
    }
}
