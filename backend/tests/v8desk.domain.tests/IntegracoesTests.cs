using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using MimeKit;
using v8desk.api.Integracoes;
using v8desk.application.Integracoes;
using v8desk.domain.Entities;
using v8desk.domain.Exceptions;
using v8desk.domain.ValueObjects;
using v8desk.infrastructure.Integracoes;
using v8desk.infrastructure.Persistence;

namespace v8desk.domain.tests;

public static class IntegracoesTests
{
    public static void ProcessadorConfirmaTesteETrataFalhasSemBanco()
    {
        var f = new Fixture();
        using var db = new V8DeskDbContext(new DbContextOptionsBuilder<V8DeskDbContext>()
            .UseNpgsql("Host=localhost;Database=modelo_apenas;Username=modelo;Password=nao_utilizada").Options, f.Cenario);
        var transporte = new TransporteFake();
        var processador = new ProcessadorEmail(db, f, f.Cenario, transporte, TimeProvider.System);
        var config = new IntegracoesEmpresa(f.Cenario.Empresa.Id);
        config.ConfigurarEmail(SmtpConfig(f));
        f.Contatos[f.Cenario.Autor.Id] = "admin@empresa.test";
        EntregaEmail Nova(bool teste = true) => new(Guid.NewGuid(), f.Cenario.Empresa.Id, f.Cenario.Autor.Id, null,
            null, teste, config.RevisaoEmail, DateTimeOffset.UtcNow);
        var entrega = Nova();
        Assert.True(processador.TentarEntregaAsync(entrega, config, CancellationToken.None).GetAwaiter().GetResult());
        Assert.Equal("AceitaPeloProvedor", entrega.Situacao);
        config.AtivarEmail(true, DateTimeOffset.UtcNow);
        Assert.Equal(1, transporte.Enviadas);
        transporte.Falha = new FalhaIntegracaoException("limite_provedor", "Informação externa", true, TimeSpan.FromSeconds(55));
        var falha = Nova();
        Assert.False(processador.TentarEntregaAsync(falha, config, CancellationToken.None).GetAwaiter().GetResult());
        Assert.Equal("Pendente", falha.Situacao); Assert.Equal(1, falha.Tentativas);
        Assert.True(falha.ProximaTentativaEm > DateTimeOffset.UtcNow.AddSeconds(50));
        Assert.Throws<RegraNegocioException>(() => config.AtivarEmail(true, DateTimeOffset.UtcNow));
        transporte.Falha = new FalhaIntegracaoException("permissao_insuficiente", "segredo externo", false);
        processador.TentarEntregaAsync(falha, config, CancellationToken.None).GetAwaiter().GetResult();
        Assert.Equal("Falha", falha.Situacao); Assert.False(JsonSerializer.Serialize(falha).Contains("segredo externo"));
        config.ConfigurarEmail(SmtpConfig(f));
        processador.TentarEntregaAsync(entrega = new(Guid.NewGuid(), f.Cenario.Empresa.Id, f.Cenario.Autor.Id, null, null, true,
            config.RevisaoEmail - 1, DateTimeOffset.UtcNow), config, CancellationToken.None).GetAwaiter().GetResult();
        Assert.Equal("Ignorada", entrega.Situacao);
        var pausada = Nova(false);
        processador.TentarEntregaAsync(pausada, config, CancellationToken.None).GetAwaiter().GetResult();
        Assert.Equal("notificacoes_pausadas", pausada.Codigo);
        var chamado = f.Cenario.Aplicacao.AbrirAsync(f.Cenario.Abertura(), "abertura-notificar").GetAwaiter().GetResult();
        var semAcesso = new Usuario(f.Cenario.Empresa.Id, "Sem vínculo");
        f.Cenario.Usuarios.Add(semAcesso); f.Contatos[semAcesso.Id] = "sem-acesso@empresa.test";
        config.RegistrarTeste(config.RevisaoEmail, true, "ok", DateTimeOffset.UtcNow);
        config.AtivarEmail(true, DateTimeOffset.UtcNow);
        var privada = new EntregaEmail(Guid.NewGuid(), f.Cenario.Empresa.Id, semAcesso.Id, Guid.NewGuid(), chamado.Id,
            false, config.RevisaoEmail, DateTimeOffset.UtcNow);
        processador.TentarEntregaAsync(privada, config, CancellationToken.None).GetAwaiter().GetResult();
        Assert.Equal("acesso_revogado", privada.Codigo);
        Assert.Equal(1, transporte.Enviadas);
        Assert.Throws<AcessoNegadoException>(() => processador.TentarEntregaAsync(
            new(Guid.NewGuid(), Guid.NewGuid(), f.Cenario.Autor.Id, null, null, true, 1, DateTimeOffset.UtcNow), config, CancellationToken.None).GetAwaiter().GetResult());
        using var cancelamento = new CancellationTokenSource(); cancelamento.Cancel();
        transporte.Falha = new OperationCanceledException(cancelamento.Token);
        Assert.Throws<OperationCanceledException>(() => processador.TentarEntregaAsync(Nova(), config, cancelamento.Token).GetAwaiter().GetResult());
    }
    public static void ConfiguracaoExigeTesteAtualENaoExponheCredenciais()
    {
        var f = new Fixture();
        f.Cenario.Administrador = true;
        var comando = new ConfigurarEmail(ProvedorEmail.Smtp, "chamados@empresa.test", "V8Desk",
            "smtp.empresa.test", 587, SegurancaSmtp.StartTls, "usuario", "segredo-teste", null, null, null);
        f.App.ExecutarAsync(comando, "config").GetAwaiter().GetResult();
        Assert.False(f.Config!.Email!.CredencialProtegida!.Contains("segredo-teste"));
        Assert.Throws<CryptographicException>(() => f.Protecao.Revelar(Guid.NewGuid(), f.Config.Email.CredencialProtegida!));
        Assert.Equal("segredo-teste", f.Protecao.Revelar(f.Cenario.Empresa.Id, f.Config.Email.CredencialProtegida!));
        var resumo = JsonSerializer.Serialize(f.App.ObterAsync().GetAwaiter().GetResult());
        Assert.False(resumo.Contains("segredo-teste")); Assert.False(resumo.Contains("CredencialProtegida"));
        Assert.Throws<RegraNegocioException>(() => f.App.ExecutarAsync(new AtivarEmail(true), "ativar").GetAwaiter().GetResult());
        f.Config.RegistrarTeste(f.Config.RevisaoEmail, true, "aceita_provedor", DateTimeOffset.UtcNow);
        f.App.ExecutarAsync(new AtivarEmail(true), "ativar-ok").GetAwaiter().GetResult();
        Assert.True(f.Config.EmailAtivo);
        var antes = f.Config.RevisaoEmail;
        var protegida = f.Config.Email.CredencialProtegida;
        f.App.ExecutarAsync(comando with { Segredo = null, NomeRemetente = "Suporte" }, "alterar").GetAwaiter().GetResult();
        Assert.Equal(protegida, f.Config.Email.CredencialProtegida);
        Assert.False(f.Config.EmailAtivo);
        f.Config.RegistrarTeste(antes, true, "aceita_provedor", DateTimeOffset.UtcNow);
        Assert.Throws<RegraNegocioException>(() => f.Config.AtivarEmail(true, DateTimeOffset.UtcNow));
        Assert.Throws<ValidacaoDominioException>(() => f.App.ExecutarAsync(comando with { HostSmtp = "outro.test", Segredo = null }, "outro").GetAwaiter().GetResult());
        Assert.Equal(f.Cenario.Solicitante.Id, f.Cenario.Autor.Id);
    }
    public static void AdministracaoReplaysContatoETesteSaoProtegidos()
    {
        var f = new Fixture();
        Assert.Throws<v8desk.domain.Exceptions.AcessoNegadoException>(() => f.App.ObterAsync().GetAwaiter().GetResult());
        f.Cenario.Administrador = true;
        f.App.ExecutarAsync(new ConfigurarEmail(ProvedorEmail.Smtp, "suporte@empresa.test", "V8Desk",
            "smtp.empresa.test", 587, SegurancaSmtp.StartTls, null, null, null, null, null), "smtp").GetAwaiter().GetResult();
        Assert.Throws<RegraNegocioException>(() => f.App.ExecutarAsync(new TestarEmail(), "teste-sem-contato").GetAwaiter().GetResult());
        f.App.ExecutarAsync(new ConfigurarContato(f.Cenario.Autor.Id, "pessoa@empresa.test"), "contato").GetAwaiter().GetResult();
        var t1 = f.App.ExecutarAsync(new TestarEmail(), "teste").GetAwaiter().GetResult();
        var t2 = f.App.ExecutarAsync(new TestarEmail(), "teste").GetAwaiter().GetResult();
        Assert.Equal(t1.RegistroId, t2.RegistroId); Assert.Equal(1, f.Testes.Count);
        Assert.True(f.Auditoria.All(x => !x.Contains("pessoa@empresa.test")));
        f.Cenario.Administrador = false;
        Assert.Throws<AcessoNegadoException>(() => f.App.ExecutarAsync(new TestarEmail(), "teste").GetAwaiter().GetResult());
    }
    public static void ValidacaoEDadosPersistidosPreservamRevisoes()
    {
        var f = new Fixture(); f.Cenario.Administrador = true;
        Assert.Throws<ValidacaoDominioException>(() => ConfiguracaoEmail.EmailValido("Nome <pessoa@empresa.test>"));
        Assert.Throws<ValidacaoDominioException>(() => ConfiguracaoEmail.EmailValido("pessoa@empresa.test\r\nBcc:x"));
        var c = new IntegracoesEmpresa(f.Cenario.Empresa.Id);
        c.ConfigurarEmail(SmtpConfig(f));
        var restaurado = JsonPersistencia.Ler<ConfiguracaoEmail>(JsonPersistencia.Escrever(c.Email));
        Assert.Equal(c.Email, restaurado);
        c.RegistrarTeste(c.RevisaoEmail, true, "ok", DateTimeOffset.UtcNow);
        c.ConfigurarEmail(c.Email! with { CredencialExpiraEm = DateTimeOffset.UtcNow.AddMinutes(-1) });
        c.RegistrarTeste(c.RevisaoEmail, true, "ok", DateTimeOffset.UtcNow);
        Assert.Throws<RegraNegocioException>(() => c.AtivarEmail(true, DateTimeOffset.UtcNow));
        var entrega = new EntregaEmail(Guid.NewGuid(), c.EmpresaId, f.Cenario.Autor.Id, null, null, true, c.RevisaoEmail, DateTimeOffset.UtcNow);
        entrega.Falhar("limite_provedor", true, TimeSpan.FromSeconds(55), DateTimeOffset.UtcNow);
        Assert.Equal("Pendente", entrega.Situacao);
        Assert.True(entrega.ProximaTentativaEm > DateTimeOffset.UtcNow.AddSeconds(50));
        for (var i = 0; i < 9; i++) entrega.Falhar("limite", true, null, DateTimeOffset.UtcNow);
        Assert.Equal("Falha", entrega.Situacao);
        entrega.Repetir(DateTimeOffset.UtcNow); Assert.Equal("Pendente", entrega.Situacao); Assert.Equal(0, entrega.Tentativas);
        entrega.Aceitar(); Assert.Equal("AceitaPeloProvedor", entrega.Situacao);
        Assert.Throws<RegraNegocioException>(() => entrega.Repetir(DateTimeOffset.UtcNow));
    }
    public static void SmtpExigeTlsPermissaoEProtegeMensagensDeFalha()
    {
        var f = new Fixture();
        var smtp = new SmtpFake();
        var transporte = new TransporteEmail(new HttpFake(), f.Protecao, new(new HashSet<string> { "smtp.empresa.test" }), smtp, TimeProvider.System);
        var config = SmtpConfig(f);
        transporte.DiagnosticarAsync(config, f.Cenario.Empresa.Id, CancellationToken.None).GetAwaiter().GetResult();
        Assert.Equal(0, smtp.Enviadas); Assert.Equal(SegurancaSmtp.StartTls, smtp.Seguranca);
        var email = new EmailSaida(Guid.NewGuid(), "pessoa@empresa.test", "Teste", "Texto");
        transporte.EnviarAsync(config, f.Cenario.Empresa.Id, email, CancellationToken.None).GetAwaiter().GetResult();
        Assert.Equal(1, smtp.Enviadas); Assert.Equal(email.Id + "@v8desk.local", smtp.Mensagem!.MessageId);
        var bloqueado = Assert.ThrowsReturning<FalhaIntegracaoException>(() => transporte.DiagnosticarAsync(
            config with { HostSmtp = "127.0.0.1" }, f.Cenario.Empresa.Id, CancellationToken.None).GetAwaiter().GetResult());
        Assert.Equal("smtp_nao_permitido", bloqueado.Codigo);
        smtp.Falha = new MailKit.Security.AuthenticationException("credencial=segredo-teste");
        var erro = Assert.ThrowsReturning<FalhaIntegracaoException>(() => transporte.DiagnosticarAsync(config, f.Cenario.Empresa.Id, CancellationToken.None).GetAwaiter().GetResult());
        Assert.False(erro.Message.Contains("segredo-teste")); Assert.False(erro.Temporaria);
        foreach (var (falha, codigo, temporaria) in new (Exception, string, bool)[]
        {
            (new TimeoutException("segredo-teste"), "provedor_timeout", true),
            (new MailKit.ServiceNotConnectedException("segredo-teste"), "provedor_indisponivel", true),
            (new MailKit.ServiceNotAuthenticatedException("segredo-teste"), "credencial_invalida", false),
            (new NotSupportedException("segredo-teste"), "smtp_incompativel", false)
        })
        {
            smtp.Falha = falha;
            var mapeada = Assert.ThrowsReturning<FalhaIntegracaoException>(() => transporte.DiagnosticarAsync(
                config, f.Cenario.Empresa.Id, CancellationToken.None).GetAwaiter().GetResult());
            Assert.Equal(codigo, mapeada.Codigo); Assert.Equal(temporaria, mapeada.Temporaria);
            Assert.False(mapeada.Message.Contains("segredo-teste"));
        }
    }
    public static void GraphEnviaSemRepetirERespeitaLimites()
    {
        var f = new Fixture(); var http = new HttpFake();
        http.Respostas.Enqueue(new(HttpStatusCode.OK) { Content = new StringContent("{\"access_token\":\"token-teste\",\"expires_in\":3600}") });
        http.Respostas.Enqueue(new(HttpStatusCode.Accepted));
        var limitado = new HttpResponseMessage(HttpStatusCode.TooManyRequests) { Content = new StringContent("detalhe-secreto") };
        limitado.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(55));
        http.Respostas.Enqueue(limitado);
        var config = new ConfiguracaoEmail
        {
            Provedor = ProvedorEmail.MicrosoftGraph,
            Remetente = "suporte@empresa.test",
            NomeRemetente = "V8Desk",
            TenantGraphId = Guid.NewGuid(),
            ClienteGraphId = Guid.NewGuid(),
            CredencialProtegida = f.Protecao.Proteger(f.Cenario.Empresa.Id, "segredo-teste")
        };
        var transporte = new TransporteEmail(http, f.Protecao, new(new HashSet<string>()), new SmtpFake(), TimeProvider.System);
        var email = new EmailSaida(Guid.NewGuid(), "pessoa@empresa.test", "Teste", "Texto");
        transporte.EnviarAsync(config, f.Cenario.Empresa.Id, email, CancellationToken.None).GetAwaiter().GetResult();
        Assert.True(http.Requisicoes[0].Url.StartsWith("https://login.microsoftonline.com/"));
        Assert.True(http.Requisicoes[0].Corpo.Contains("client_credentials"));
        Assert.True(http.Requisicoes[1].Url.StartsWith("https://graph.microsoft.com/v1.0/users/"));
        Assert.True(http.Requisicoes[1].Corpo.Contains(email.Id.ToString()));
        var erro = Assert.ThrowsReturning<FalhaIntegracaoException>(() => transporte.EnviarAsync(config, f.Cenario.Empresa.Id, email, CancellationToken.None).GetAwaiter().GetResult());
        Assert.Equal("limite_provedor", erro.Codigo); Assert.True(erro.Temporaria); Assert.Equal(TimeSpan.FromSeconds(55), erro.Aguardar);
        Assert.Equal(3, http.Requisicoes.Count); Assert.False(erro.Message.Contains("detalhe-secreto"));
    }
    public static void GraphFalhasPermanentesNaoVazamResposta()
    {
        foreach (var (status, codigo, temporaria) in new[] {
            (HttpStatusCode.Forbidden, "permissao_insuficiente", false), (HttpStatusCode.NotFound, "caixa_indisponivel", false),
            (HttpStatusCode.ServiceUnavailable, "provedor_indisponivel", true) })
        {
            var f = new Fixture(); var http = new HttpFake();
            http.Respostas.Enqueue(new(HttpStatusCode.OK) { Content = new StringContent("{\"access_token\":\"token\"}") });
            http.Respostas.Enqueue(new(status) { Content = new StringContent("secret=interno") });
            var config = new ConfiguracaoEmail
            {
                Provedor = ProvedorEmail.MicrosoftGraph,
                Remetente = "suporte@empresa.test",
                NomeRemetente = "V8Desk",
                TenantGraphId = Guid.NewGuid(),
                ClienteGraphId = Guid.NewGuid(),
                CredencialProtegida = f.Protecao.Proteger(f.Cenario.Empresa.Id, "segredo")
            };
            var transporte = new TransporteEmail(http, f.Protecao, new(new HashSet<string>()), new SmtpFake(), TimeProvider.System);
            var erro = Assert.ThrowsReturning<FalhaIntegracaoException>(() => transporte.EnviarAsync(config, f.Cenario.Empresa.Id,
                new(Guid.NewGuid(), "pessoa@empresa.test", "Teste", "Texto"), CancellationToken.None).GetAwaiter().GetResult());
            Assert.Equal(codigo, erro.Codigo); Assert.Equal(temporaria, erro.Temporaria); Assert.False(erro.ToString().Contains("interno"));
        }
    }
    private static ConfiguracaoEmail SmtpConfig(Fixture f) => new()
    {
        Provedor = ProvedorEmail.Smtp,
        Remetente = "suporte@empresa.test",
        NomeRemetente = "V8Desk",
        HostSmtp = "smtp.empresa.test",
        PortaSmtp = 587,
        SegurancaSmtp = SegurancaSmtp.StartTls,
        UsuarioSmtp = "usuario",
        CredencialProtegida = f.Protecao.Proteger(f.Cenario.Empresa.Id, "segredo-teste")
    };
    internal sealed class Fixture : IIntegracoesRepository, IIdentidadeMicrosoftRepository
    {
        internal readonly AplicacaoTests.Cenario Cenario = new();
        internal readonly IProtecaoCredencial Protecao = new ProtecaoCredencial(new EphemeralDataProtectionProvider());
        internal IntegracoesEmpresa? Config;
        internal readonly Dictionary<Guid, string> Contatos = [];
        internal readonly List<Guid> Testes = [];
        internal readonly List<string> Auditoria = [];
        internal Guid Objeto = Guid.NewGuid();
        internal bool AdministradorMicrosoft;
        internal readonly ITransporteEmail Transporte = new TransporteFake();
        internal IntegracoesAplicacao App => new(this, Cenario, Cenario, Protecao, Transporte, Cenario, Cenario, Cenario, Cenario, TimeProvider.System);
        public Task<IntegracoesEmpresa?> ObterAsync(bool atualizar, CancellationToken ct) => Task.FromResult(Config);
        public void Adicionar(IntegracoesEmpresa c) => Config = c;
        public Task VincularAsync(Guid usuarioId, Guid tenantId, Guid objetoId, bool administrador, bool ativo, CancellationToken ct)
        { Objeto = objetoId; AdministradorMicrosoft = administrador; return Task.CompletedTask; }
        public Task ContatoAsync(Guid usuarioId, string email, CancellationToken ct) { Contatos[usuarioId] = email; return Task.CompletedTask; }
        public Task<string?> EmailAsync(Guid usuarioId, CancellationToken ct) => Task.FromResult(Contatos.GetValueOrDefault(usuarioId));
        public Task RegistrarTesteAsync(Guid id, Guid usuarioId, long revisao, CancellationToken ct) { Testes.Add(id); return Task.CompletedTask; }
        public Task<IReadOnlyList<EntregaResumo>> EntregasAsync(int tamanho, CancellationToken ct) => Task.FromResult<IReadOnlyList<EntregaResumo>>([]);
        public Task RepetirEntregaAsync(Guid id, CancellationToken ct) => Task.CompletedTask;
        public void Auditar(Guid usuarioId, string acao, DateTimeOffset agora) => Auditoria.Add(acao);
        public Task<IdentidadeMicrosoft?> ResolverAsync(Guid tenant, Guid api, Guid objeto, CancellationToken ct) =>
            Task.FromResult<IdentidadeMicrosoft?>(Config?.Microsoft is { } c && c.TenantId == tenant && c.ApiClienteId == api && objeto == Objeto
                ? new(Cenario.Empresa.Id, Cenario.Autor.Id, AdministradorMicrosoft, Config.MicrosoftAtivo && Cenario.Autor.Ativo) : null);
        public Task<AcessoMicrosoft?> AcessoAsync(Guid empresa, CancellationToken ct) =>
            Task.FromResult<AcessoMicrosoft?>(empresa == Cenario.Empresa.Id && Config?.MicrosoftAtivo == true && Config.Microsoft is { } c
                ? new(c.TenantId, c.ApiClienteId, c.ClienteLoginId, "https://login.microsoftonline.com/" + c.TenantId + "/v2.0", "api://" + c.ApiClienteId + "/access_as_user") : null);
    }
    private sealed class TransporteFake : ITransporteEmail
    {
        internal int Enviadas;
        internal Exception? Falha;
        public Task<DiagnosticoIntegracao> DiagnosticarAsync(ConfiguracaoEmail c, Guid e, CancellationToken ct) =>
            Task.FromResult(new DiagnosticoIntegracao(true, "ok", "Conexão validada."));
        public Task EnviarAsync(ConfiguracaoEmail c, Guid e, EmailSaida email, CancellationToken ct)
        {
            if (Falha is not null) return Task.FromException(Falha);
            Enviadas++; return Task.CompletedTask;
        }
    }
    private sealed class SmtpFake : ISessaoSmtpFactory, ISessaoSmtp
    {
        internal int Enviadas; internal SegurancaSmtp Seguranca; internal MimeMessage? Mensagem; internal Exception? Falha;
        public ISessaoSmtp Criar() => this;
        public Task ConectarAsync(string host, int porta, SegurancaSmtp seguranca, CancellationToken ct) { Seguranca = seguranca; return Task.CompletedTask; }
        public Task AutenticarAsync(string usuario, string senha, CancellationToken ct) => Falha is null ? Task.CompletedTask : Task.FromException(Falha);
        public Task EnviarAsync(MimeMessage m, CancellationToken ct) { Enviadas++; Mensagem = m; return Task.CompletedTask; }
        public Task DesconectarAsync(CancellationToken ct) => Task.CompletedTask;
        public void Dispose() { }
    }
    private sealed class HttpFake : HttpMessageHandler, IHttpClientFactory
    {
        internal readonly Queue<HttpResponseMessage> Respostas = [];
        internal readonly List<(string Url, string Corpo)> Requisicoes = [];
        public HttpClient CreateClient(string name) => new(this, disposeHandler: false);
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        { Requisicoes.Add((request.RequestUri!.ToString(), request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct))); return Respostas.Dequeue(); }
    }
}
