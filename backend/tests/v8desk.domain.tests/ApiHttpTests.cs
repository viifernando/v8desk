using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using v8desk.api.Controllers;
using v8desk.application.Abstractions;
using v8desk.application.Chamados;
using v8desk.domain.Exceptions;
using Microsoft.IdentityModel.Tokens;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Protocols;
using System.IdentityModel.Tokens.Jwt;

namespace v8desk.domain.tests;

public static class ApiHttpTests
{
    public static void CriacoesRetornam201EOpenApiDocumentaRespostas() => VerificarCriacoesAsync().GetAwaiter().GetResult();

    private static async Task VerificarCriacoesAsync()
    {
        using var host = new HostApi();
        host.Cenario.Autor = host.Cenario.Atendente;
        host.Cenario.VinculoAtendente.ConcederPapel(v8desk.domain.Enums.PapelSetor.Gestor);
        using var client = host.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        Entrar(client, host.Cenario);
        client.DefaultRequestHeaders.Add("X-Admin-Teste", "sim");
        var setor = host.Cenario.Atendimento.Id;
        var rotas = new (string Rota, object Corpo)[]
        {
            ("/api/v1/empresa/setores", new { nome = "Financeiro HTTP" }),
            ("/api/v1/empresa/usuarios", new { nome = "Usuário HTTP" }),
            ($"/api/v1/setores/{setor}/filas", new { nome = "Fila HTTP" }),
            ($"/api/v1/setores/{setor}/categorias", new { nome = "Categoria HTTP" }),
            ($"/api/v1/setores/{setor}/vinculos", new { usuarioId = host.Cenario.Solicitante.Id, papel = "Atendente" })
        };
        for (var i = 0; i < rotas.Length; i++)
        {
            client.DefaultRequestHeaders.Remove("Idempotency-Key");
            client.DefaultRequestHeaders.Add("Idempotency-Key", "criacao-http-" + i);
            using var criada = await client.PostAsJsonAsync(rotas[i].Rota, rotas[i].Corpo, Json);
            Assert.Equal(HttpStatusCode.Created, criada.StatusCode);
            var corpo = await criada.Content.ReadAsStringAsync();
            using var resultado = JsonDocument.Parse(corpo);
            Assert.True(resultado.RootElement.GetProperty("registroId").GetGuid() != Guid.Empty);
            using var replay = await client.PostAsJsonAsync(rotas[i].Rota, rotas[i].Corpo, Json);
            Assert.Equal(HttpStatusCode.Created, replay.StatusCode);
            Assert.Equal(corpo, await replay.Content.ReadAsStringAsync());
        }
        using var documento = await client.GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.OK, documento.StatusCode);
        using var openapi = JsonDocument.Parse(await documento.Content.ReadAsStringAsync());
        var paths = openapi.RootElement.GetProperty("paths");
        foreach (var rota in new[] { "/api/v1/empresa/setores", "/api/v1/empresa/usuarios",
            "/api/v1/setores/{setorId}/filas", "/api/v1/setores/{setorId}/categorias", "/api/v1/setores/{setorId}/vinculos" })
        {
            var respostas = paths.GetProperty(rota).GetProperty("post").GetProperty("responses");
            Assert.True(respostas.TryGetProperty("201", out _));
            Assert.False(respostas.TryGetProperty("200", out _));
            foreach (var status in new[] { "400", "401", "403", "409", "413", "415", "429", "500", "503" })
                Assert.True(respostas.TryGetProperty(status, out _));
        }
        var teste = paths.GetProperty("/api/v1/integracoes/email/testar").GetProperty("post").GetProperty("responses");
        Assert.True(teste.TryGetProperty("202", out _)); Assert.False(teste.TryGetProperty("200", out _));
        Assert.True(paths.GetProperty("/api/v1/acesso/microsoft/{empresaId}").GetProperty("get")
            .GetProperty("responses").TryGetProperty("404", out _));
    }
    public static void AutenticacaoValidacaoEIdempotencia() => ExecutarFluxoAsync().GetAwaiter().GetResult();
    public static void LimitesEIndisponibilidade() => ExecutarLimitesAsync().GetAwaiter().GetResult();
    public static void PrazoCancelaConsultaSemExporDetalhes() => ExecutarPrazoAsync().GetAwaiter().GetResult();
    public static void JwtValidaAssinaturaEmissorPublicoPrazoEEmpresa() => ExecutarJwtAsync().GetAwaiter().GetResult();

    private static async Task ExecutarJwtAsync()
    {
        using var host = new HostApi(jwt: true);
        using var client = host.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        var destino = "/api/v1/chamados?filaId=" + host.Cenario.Atendimento.FilaGeralId;
        string Token(string issuer = "https://issuer.v8desk.test", string audience = "v8desk-api",
            SecurityKey? chave = null, bool vencido = false, string? empresa = null, bool semEmpresa = false)
        {
            var claims = new List<Claim> { new("sub", host.Cenario.Autor.Id.ToString()) };
            if (!semEmpresa) claims.Add(new("empresa_id", empresa ?? host.Cenario.Empresa.Id.ToString()));
            var token = new JwtSecurityToken(issuer, audience, claims, DateTime.UtcNow.AddHours(-1),
                vencido ? DateTime.UtcNow.AddMinutes(-5) : DateTime.UtcNow.AddMinutes(5),
                new SigningCredentials(chave ?? host.ChaveJwt, SecurityAlgorithms.RsaSha256));
            return new JwtSecurityTokenHandler().WriteToken(token);
        }
        using var rsaIncorreto = System.Security.Cryptography.RSA.Create(2048);
        foreach (var token in new[]
        {
            Token(issuer: "https://outro.test"), Token(audience: "outro"), Token(vencido: true),
            Token(chave: new RsaSecurityKey(rsaIncorreto))
        })
        {
            client.DefaultRequestHeaders.Authorization = new("Bearer", token);
            var rejeitada = await client.GetAsync(destino);
            await Problema(rejeitada, 401, "autenticacao_necessaria");
            Assert.True(rejeitada.Headers.WwwAuthenticate.Any(h => h.Scheme == "Bearer"));
        }
        client.DefaultRequestHeaders.Authorization = new("Bearer", Token(semEmpresa: true));
        await Problema(await client.GetAsync(destino), 403, "acesso_negado");
        client.DefaultRequestHeaders.Authorization = new("Bearer", Token());
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(destino)).StatusCode);
        client.DefaultRequestHeaders.Authorization = new("Bearer", Token(empresa: Guid.NewGuid().ToString()));
        client.DefaultRequestHeaders.Add("Idempotency-Key", "outra-empresa");
        await Problema(await client.PostAsJsonAsync("/api/v1/chamados", host.Cenario.Abertura(), Json), 403, "acesso_negado");
        Assert.Equal(0, host.Cenario.Salvamentos);
    }

    private static async Task ExecutarPrazoAsync()
    {
        using var host = new HostApi(prazo: 1);
        using var client = host.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        Entrar(client, host.Cenario);
        host.Consulta.AguardarCancelamento = true;
        var resultado = await client.GetAsync("/api/v1/chamados?filaId=" + host.Cenario.Atendimento.FilaGeralId);
        await Problema(resultado, 503, "prazo_requisicao_excedido");
        Assert.True(host.Consulta.Cancelada);
    }

    private static async Task ExecutarFluxoAsync()
    {
        using var host = new HostApi();
        using var client = host.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        await Problema(await client.GetAsync("/api/v1/chamados"), 401, "autenticacao_necessaria");
        Entrar(client, host.Cenario);
        client.DefaultRequestHeaders.Remove("X-Empresa-Teste");
        client.DefaultRequestHeaders.Add("X-Empresa-Teste", "invalida");
        await Problema(await client.GetAsync("/api/v1/chamados"), 403, "acesso_negado");
        Entrar(client, host.Cenario);
        var abertura = host.Cenario.Abertura();
        await Problema(await client.PostAsync("/api/v1/chamados", new StringContent("{", Encoding.UTF8, "application/json")),
            400, "requisicao_invalida");
        await Problema(await client.PostAsync("/api/v1/chamados", new StringContent("{}", Encoding.UTF8, "application/json")),
            400, "requisicao_invalida");
        await Problema(await client.PostAsJsonAsync("/api/v1/chamados", abertura with { Titulo = " " }, Json),
            400, "requisicao_invalida");
        await Problema(await client.PostAsJsonAsync("/api/v1/chamados", abertura with { CategoriaId = Guid.Empty }, Json),
            400, "requisicao_invalida");
        await Problema(await client.PostAsync("/api/v1/chamados", new StringContent(
            JsonSerializer.Serialize(abertura, Json).Replace("\"Baixa\"", "99").Replace("\"Media\"", "99"), Encoding.UTF8, "application/json")),
            400, "requisicao_invalida");
        await Problema(await client.PostAsync("/api/v1/chamados", new StringContent(
            JsonSerializer.Serialize(abertura, Json).TrimEnd('}') + ",\"empresaId\":\"00000000-0000-0000-0000-000000000001\"}",
            Encoding.UTF8, "application/json")), 400, "requisicao_invalida");
        await Problema(await client.PostAsJsonAsync("/api/v1/chamados", abertura, Json), 400, "chave_invalida");
        await Problema(await client.PostAsync("/api/v1/chamados", new StringContent("texto", Encoding.UTF8, "text/plain")),
            415, "formato_nao_aceito");
        await Problema(await client.PostAsync("/api/v1/chamados", new StringContent(new string('x', 129 * 1024), Encoding.UTF8, "application/json")),
            413, "requisicao_grande");
        await Problema(await client.PostAsJsonAsync("/api/v1/empresa/setores", new { nome = "Novo" }), 403, "acesso_negado");
        client.DefaultRequestHeaders.Add("Idempotency-Key", "abertura-http");
        using var primeira = await client.PostAsJsonAsync("/api/v1/chamados", abertura, Json);
        Assert.Equal(HttpStatusCode.Created, primeira.StatusCode);
        Assert.True(primeira.Headers.Location is not null);
        var resultado = await primeira.Content.ReadFromJsonAsync<ResultadoChamado>(Json);
        using var replay = await client.PostAsJsonAsync("/api/v1/chamados", abertura, Json);
        Assert.Equal(HttpStatusCode.Created, replay.StatusCode);
        Assert.Equal(resultado!.Id, (await replay.Content.ReadFromJsonAsync<ResultadoChamado>(Json))!.Id);
        Assert.Equal(1, host.Cenario.Salvamentos);
        await Problema(await client.PostAsJsonAsync("/api/v1/chamados", abertura with { Titulo = "Outro" }, Json),
            409, "regra_negocio");
        await Problema(await client.GetAsync("/api/v1/chamados?tamanho=101&filaId=" + host.Cenario.Atendimento.FilaGeralId),
            400, "requisicao_invalida");
        await Problema(await client.GetAsync("/api/v1/chamados?filaId=" + host.Cenario.Atendimento.FilaGeralId
            + "&situacao=99"), 400, "requisicao_invalida");
        using var consulta = await client.GetAsync("/api/v1/chamados?filaId=" + host.Cenario.Atendimento.FilaGeralId);
        Assert.Equal(HttpStatusCode.OK, consulta.StatusCode);
        Assert.Equal("no-store", consulta.Headers.CacheControl!.ToString());
        Assert.Equal(host.Cenario.Empresa.Id, host.Consulta.EmpresaObservada);
        host.Consulta.Falha = new InvalidOperationException("Senha=segredo interno");
        await Problema(await client.GetAsync("/api/v1/chamados?filaId=" + host.Cenario.Atendimento.FilaGeralId), 500, "erro_inesperado");
        host.Consulta.Falha = new TimeoutException("host interno");
        var indisponivel = await client.GetAsync("/api/v1/chamados?filaId=" + host.Cenario.Atendimento.FilaGeralId);
        await Problema(indisponivel, 503, "servico_indisponivel");
        Assert.True(indisponivel.Headers.RetryAfter is not null);
        await Problema(await client.GetAsync("/api/v1/inexistente"), 404, "rota_nao_encontrada");
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live")).StatusCode);
        await Problema(await client.GetAsync("/health/ready"), 503, "servico_indisponivel");
        using var openapi = await client.GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.OK, openapi.StatusCode);
        using var contrato = JsonDocument.Parse(await openapi.Content.ReadAsStringAsync());
        Assert.True(contrato.RootElement.GetProperty("paths").TryGetProperty("/api/v1/chamados/{id}/mensagens", out _));
    }

    private static async Task ExecutarLimitesAsync()
    {
        using var host = new HostApi(limite: 1);
        using var client = host.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        Entrar(client, host.Cenario);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/chamados?filaId=" + host.Cenario.Atendimento.FilaGeralId)).StatusCode);
        var excesso = await client.GetAsync("/api/v1/chamados?filaId=" + host.Cenario.Atendimento.FilaGeralId);
        await Problema(excesso, 429, "limite_requisicoes");
        Assert.True(excesso.Headers.RetryAfter is not null);
        using var semBanco = new HostApi(persistencia: false);
        using var semBancoClient = semBanco.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        Entrar(semBancoClient, semBanco.Cenario);
        await Problema(await semBancoClient.GetAsync("/api/v1/chamados"), 503, "servico_indisponivel");
        using var semProvedor = new HostApi(persistencia: false, autenticarTeste: false);
        using var semProvedorClient = semProvedor.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        Entrar(semProvedorClient, semProvedor.Cenario);
        await Problema(await semProvedorClient.GetAsync("/api/v1/chamados"), 401, "autenticacao_necessaria");
    }

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    { Converters = { new JsonStringEnumConverter(allowIntegerValues: false) } };
    private static void Entrar(HttpClient client, AplicacaoTests.Cenario cenario)
    {
        client.DefaultRequestHeaders.Remove("X-Usuario-Teste");
        client.DefaultRequestHeaders.Remove("X-Empresa-Teste");
        client.DefaultRequestHeaders.Add("X-Usuario-Teste", cenario.Autor.Id.ToString());
        client.DefaultRequestHeaders.Add("X-Empresa-Teste", cenario.Empresa.Id.ToString());
    }
    private static async Task Problema(HttpResponseMessage response, int status, string codigo)
    {
        Assert.Equal(status, (int)response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
        var texto = await response.Content.ReadAsStringAsync();
        Assert.False(texto.Contains("segredo"));
        using var json = JsonDocument.Parse(texto);
        Assert.Equal(codigo, json.RootElement.GetProperty("code").GetString());
        Assert.True(json.RootElement.TryGetProperty("traceId", out _));
    }

    private sealed class HostApi(int limite = 120, bool persistencia = true, int prazo = 60, bool jwt = false,
        bool autenticarTeste = true) : WebApplicationFactory<ChamadosController>
    {
        internal readonly AplicacaoTests.Cenario Cenario = new();
        internal readonly ConsultaFalsa Consulta = new();
        private readonly System.Security.Cryptography.RSA _rsa = System.Security.Cryptography.RSA.Create(2048);
        internal RsaSecurityKey ChaveJwt => new(_rsa) { KeyId = "chave-teste" };
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing) _rsa.Dispose();
        }
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseContentRoot(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/v8desk.api")));
            builder.UseEnvironment("Development");
            builder.UseSetting("ConnectionStrings:V8Desk", persistencia ? "Host=localhost;Database=v8desk_test;Username=teste;Password=teste" : "");
            builder.UseSetting("Authentication:Authority", jwt ? "https://issuer.v8desk.test" : "");
            builder.UseSetting("Authentication:Audience", jwt ? "v8desk-api" : "");
            builder.UseSetting("Api:RequisicoesPorMinuto", limite.ToString());
            builder.UseSetting("Api:PrazoRequisicaoSegundos", prazo.ToString());
            builder.UseSetting("Logging:LogLevel:Default", "Error");
            builder.ConfigureTestServices(services =>
            {
                services.AddLogging(options => options.ClearProviders());
                if (jwt)
                    services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
                    {
                        options.Configuration = new OpenIdConnectConfiguration { Issuer = "https://issuer.v8desk.test" };
                        options.Configuration.SigningKeys.Add(ChaveJwt);
                        options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(options.Configuration);
                    });
                else if (autenticarTeste) services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = "teste";
                    options.DefaultChallengeScheme = "teste";
                    options.DefaultForbidScheme = "teste";
                }).AddScheme<AuthenticationSchemeOptions, IdentidadeTeste>("teste", _ => { });
                services.RemoveAll<IAcessoRepository>(); services.AddSingleton<IAcessoRepository>(Cenario);
                services.RemoveAll<IConfiguracaoRepository>(); services.AddSingleton<IConfiguracaoRepository>(Cenario);
                services.RemoveAll<IChamadoRepository>(); services.AddSingleton<IChamadoRepository>(Cenario);
                services.RemoveAll<IExecutorComandoIdempotente>(); services.AddSingleton<IExecutorComandoIdempotente>(Cenario);
                services.RemoveAll<IUnidadeTrabalho>(); services.AddSingleton<IUnidadeTrabalho>(Cenario);
                services.RemoveAll<IChamadoConsultas>();
                services.AddScoped<IChamadoConsultas>(sp =>
                {
                    Consulta.EmpresaObservada = sp.GetRequiredService<IEmpresaAtual>().EmpresaId;
                    return Consulta;
                });
            });
        }
    }
    private sealed class ConsultaFalsa : IChamadoConsultas
    {
        public Guid EmpresaObservada { get; set; }
        public Exception? Falha { get; set; }
        public bool AguardarCancelamento { get; set; }
        public bool Cancelada { get; private set; }
        public async Task<PaginaChamados> PesquisarAsync(FiltroChamados filtro, string? cursor = null, int tamanho = 25, CancellationToken cancellationToken = default)
        {
            if (AguardarCancelamento)
            {
                try { await Task.Delay(Timeout.Infinite, cancellationToken); }
                catch (OperationCanceledException) { Cancelada = true; throw; }
            }
            if (Falha is not null) throw Falha;
            return new PaginaChamados([], null, 0, false);
        }
        public Task<IReadOnlyList<ChamadoResumo>> ListarFilaAsync(Guid filaId, CursorChamados? depois = null, int tamanho = 50,
            CancellationToken cancellationToken = default, Guid? categoriaId = null) => throw new NotSupportedException();
    }
    // Headers de identidade existem somente neste assembly de testes.
    public sealed class IdentidadeTeste(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger,
        UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue("X-Usuario-Teste", out var usuario))
                return Task.FromResult(AuthenticateResult.NoResult());
            var principal = new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim("sub", usuario.ToString()), new Claim("empresa_id", Request.Headers["X-Empresa-Teste"].ToString()) }
                    .Concat(Request.Headers["X-Admin-Teste"] == "sim" ? [new Claim(ClaimTypes.Role, "administrador_empresa")] : []), Scheme.Name));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name)));
        }
    }
}
