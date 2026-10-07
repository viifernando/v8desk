using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MailKit.Security;
using MimeKit;
using v8desk.application.Integracoes;
using v8desk.domain.ValueObjects;

namespace v8desk.infrastructure.Integracoes;

public sealed record OpcoesEmail(IReadOnlySet<string> HostsSmtpPermitidos);
public interface ISessaoSmtp : IDisposable
{
    Task ConectarAsync(string host, int porta, SegurancaSmtp seguranca, CancellationToken ct);
    Task AutenticarAsync(string usuario, string senha, CancellationToken ct);
    Task EnviarAsync(MimeMessage mensagem, CancellationToken ct);
    Task DesconectarAsync(CancellationToken ct);
}
public interface ISessaoSmtpFactory { ISessaoSmtp Criar(); }
public sealed class SessaoSmtpFactory : ISessaoSmtpFactory
{
    public ISessaoSmtp Criar() => new Sessao();
    private sealed class Sessao : ISessaoSmtp
    {
        private readonly MailKit.Net.Smtp.SmtpClient _cliente = new() { Timeout = 15000 };
        public Task ConectarAsync(string host, int porta, SegurancaSmtp seguranca, CancellationToken ct) =>
            _cliente.ConnectAsync(host, porta, seguranca == SegurancaSmtp.TlsImplicito ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls, ct);
        public Task AutenticarAsync(string usuario, string senha, CancellationToken ct) => _cliente.AuthenticateAsync(usuario, senha, ct);
        public async Task EnviarAsync(MimeMessage mensagem, CancellationToken ct) => await _cliente.SendAsync(mensagem, ct);
        public Task DesconectarAsync(CancellationToken ct) => _cliente.DisconnectAsync(true, ct);
        public void Dispose() => _cliente.Dispose();
    }
}
public sealed class TransporteEmail(IHttpClientFactory http, IProtecaoCredencial protecao, OpcoesEmail opcoes, ISessaoSmtpFactory smtp, TimeProvider relogio) : ITransporteEmail
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<Guid, TokenGraph> Tokens = new();
    private sealed record TokenGraph(string Configuracao, string Valor, DateTimeOffset ExpiraEm);
    public async Task<DiagnosticoIntegracao> DiagnosticarAsync(ConfiguracaoEmail config, Guid empresaId, CancellationToken ct)
    {
        await ExecutarAsync(async () =>
        {
            ValidarExpiracao(config);
            if (config.Provedor == ProvedorEmail.MicrosoftGraph) await TokenAsync(config, empresaId, ct);
            else await SmtpAsync(config, empresaId, null, ct);
        }, ct);
        return new(true, "credencial_validada", config.Provedor == ProvedorEmail.MicrosoftGraph
            ? "Credencial Microsoft validada. Envie um teste para confirmar a permissão da caixa de e-mail."
            : "Conexão SMTP validada. Envie um teste para confirmar o envio.");
    }
    public Task EnviarAsync(ConfiguracaoEmail config, Guid empresaId, EmailSaida email, CancellationToken ct) =>
        ExecutarAsync(async () =>
        {
            ValidarExpiracao(config);
            if (config.Provedor == ProvedorEmail.Smtp) { await SmtpAsync(config, empresaId, email, ct); return; }
            var token = await TokenAsync(config, empresaId, ct);
            using var req = new HttpRequestMessage(HttpMethod.Post, "https://graph.microsoft.com/v1.0/users/" + Uri.EscapeDataString(config.Remetente) + "/sendMail");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            req.Content = JsonContent.Create(new
            {
                message = new
                {
                    subject = email.Assunto, body = new { contentType = "Text", content = email.Texto },
                    toRecipients = new[] { new { emailAddress = new { address = email.Destinatario } } },
                    internetMessageHeaders = new[] { new { name = "x-v8desk-entrega", value = email.Id.ToString() } }
                },
                saveToSentItems = true
            });
            using var client = http.CreateClient("integracoes");
            using var response = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
            if (response.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden)
                Tokens.TryRemove(empresaId, out _);
            VerificarResposta(response);
            if (response.StatusCode != System.Net.HttpStatusCode.Accepted)
                throw new FalhaIntegracaoException("resposta_invalida", "O provedor não confirmou a aceitação do envio. Tente novamente.", true);
        }, ct);
    private async Task SmtpAsync(ConfiguracaoEmail config, Guid empresa, EmailSaida? email, CancellationToken ct)
    {
        if (config.HostSmtp is null || !opcoes.HostsSmtpPermitidos.Contains(config.HostSmtp))
            throw new FalhaIntegracaoException("smtp_nao_permitido", "Este servidor SMTP não está liberado na configuração do servidor V8Desk.", false);
        using var sessao = smtp.Criar();
        await sessao.ConectarAsync(config.HostSmtp, config.PortaSmtp, config.SegurancaSmtp, ct);
        if (!string.IsNullOrEmpty(config.UsuarioSmtp))
            await sessao.AutenticarAsync(config.UsuarioSmtp, Revelar(empresa, config), ct);
        if (email is not null)
        {
            var mensagem = new MimeMessage { Subject = email.Assunto, MessageId = email.Id + "@v8desk.local" };
            mensagem.From.Add(new MailboxAddress(config.NomeRemetente, config.Remetente));
            mensagem.To.Add(MailboxAddress.Parse(email.Destinatario));
            mensagem.Body = new TextPart("plain") { Text = email.Texto };
            await sessao.EnviarAsync(mensagem, ct);
        }
        try { await sessao.DesconectarAsync(ct); }
        catch (Exception) when (!ct.IsCancellationRequested) { }
    }
    private async Task<string> TokenAsync(ConfiguracaoEmail config, Guid empresa, CancellationToken ct)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(config.TenantGraphId + ":" + config.ClienteGraphId + ":" + config.CredencialProtegida)));
        if (Tokens.TryGetValue(empresa, out var cache) && cache.Configuracao == hash && cache.ExpiraEm > relogio.GetUtcNow().AddMinutes(1))
            return cache.Valor;
        using var req = new HttpRequestMessage(HttpMethod.Post, "https://login.microsoftonline.com/" + config.TenantGraphId + "/oauth2/v2.0/token")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = config.ClienteGraphId.ToString()!, ["client_secret"] = Revelar(empresa, config),
                ["grant_type"] = "client_credentials", ["scope"] = "https://graph.microsoft.com/.default"
            })
        };
        using var client = http.CreateClient("integracoes");
        using var response = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
        VerificarResposta(response, token: true);
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        var buffer = new byte[65537];
        var bytes = 0;
        while (bytes < buffer.Length)
        {
            var lidos = await stream.ReadAsync(buffer.AsMemory(bytes), ct);
            if (lidos == 0) break; bytes += lidos;
        }
        if (bytes == buffer.Length) throw new FalhaIntegracaoException("resposta_invalida", "Não foi possível validar a credencial Microsoft.", true);
        using var json = JsonDocument.Parse(buffer.AsMemory(0, bytes));
        if (!json.RootElement.TryGetProperty("access_token", out var valor) || valor.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(valor.GetString()))
            throw new FalhaIntegracaoException("resposta_invalida", "Não foi possível validar a credencial Microsoft.", true);
        var segundos = json.RootElement.TryGetProperty("expires_in", out var exp) && exp.ValueKind == JsonValueKind.Number && exp.TryGetInt32(out var n) ? Math.Clamp(n, 1, 3600) : 300;
        var token = valor.GetString()!;
        Tokens[empresa] = new(hash, token, relogio.GetUtcNow().AddSeconds(segundos));
        return token;
    }
    private static void VerificarResposta(HttpResponseMessage response, bool token = false)
    {
        if (response.IsSuccessStatusCode) return;
        var espera = response.Headers.RetryAfter?.Delta ?? (response.Headers.RetryAfter?.Date - DateTimeOffset.UtcNow);
        var (codigo, mensagem, temporaria) = (int)response.StatusCode switch
        {
            429 => ("limite_provedor", "O provedor limitou os envios. A entrega será tentada novamente após a espera indicada.", true),
            401 or 400 when token => ("credencial_invalida", "A credencial Microsoft não foi aceita. Confira o aplicativo, o tenant e a validade da credencial.", false),
            401 => ("credencial_invalida", "A credencial de envio não foi aceita. Confira a configuração.", false),
            403 => ("permissao_insuficiente", "O aplicativo não tem permissão para enviar por esta caixa. Confira o consentimento e o escopo de acesso no Exchange.", false),
            404 => ("caixa_indisponivel", "A caixa de e-mail não está disponível. Confira o remetente.", false),
            >= 500 => ("provedor_indisponivel", "O provedor está temporariamente indisponível. A entrega será tentada novamente.", true),
            _ => ("envio_rejeitado", "O provedor não aceitou o envio. Confira a configuração e o destinatário.", false)
        };
        throw new FalhaIntegracaoException(codigo, mensagem, temporaria, espera);
    }
    private string Revelar(Guid empresa, ConfiguracaoEmail config)
    {
        try { return protecao.Revelar(empresa, config.CredencialProtegida ?? ""); }
        catch (CryptographicException) { throw new FalhaIntegracaoException("credencial_indisponivel", "Não foi possível acessar a credencial. Confira as chaves de proteção do servidor ou cadastre uma nova credencial.", false); }
    }
    private void ValidarExpiracao(ConfiguracaoEmail config)
    {
        config.Validar();
        if (config.CredencialExpiraEm <= relogio.GetUtcNow())
            throw new FalhaIntegracaoException("credencial_expirada", "A credencial expirou. Atualize-a e teste novamente.", false);
    }
    private static async Task ExecutarAsync(Func<Task> executar, CancellationToken ct)
    {
        try { ct.ThrowIfCancellationRequested(); await executar(); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw new FalhaIntegracaoException("provedor_timeout", "O provedor demorou para responder. Tente novamente.", true); }
        catch (TimeoutException) { throw new FalhaIntegracaoException("provedor_timeout", "O provedor demorou para responder. Tente novamente.", true); }
        catch (MailKit.ServiceNotAuthenticatedException) { throw new FalhaIntegracaoException("credencial_invalida", "O servidor SMTP exige autenticação. Confira o usuário e a senha.", false); }
        catch (MailKit.ServiceNotConnectedException) { throw new FalhaIntegracaoException("provedor_indisponivel", "A conexão com o servidor SMTP foi interrompida. Tente novamente.", true); }
        catch (NotSupportedException) { throw new FalhaIntegracaoException("smtp_incompativel", "O servidor SMTP não oferece a segurança ou autenticação configurada. Confira a configuração.", false); }
        catch (MailKit.Security.AuthenticationException) { throw new FalhaIntegracaoException("credencial_invalida", "O servidor SMTP não aceitou a credencial. Confira o usuário e a senha.", false); }
        catch (SslHandshakeException) { throw new FalhaIntegracaoException("tls_invalido", "Não foi possível estabelecer uma conexão segura. Confira o certificado, a porta e a segurança do servidor SMTP.", false); }
        catch (MailKit.Net.Smtp.SmtpCommandException e)
        { throw new FalhaIntegracaoException("smtp_rejeitado", "O servidor SMTP não aceitou o envio. Confira remetente, destinatário e permissões.", (int)e.StatusCode is >= 400 and < 500); }
        catch (Exception e) when (e is HttpRequestException or IOException or System.Net.Sockets.SocketException or MailKit.Net.Smtp.SmtpProtocolException)
        { throw new FalhaIntegracaoException("provedor_indisponivel", "Não foi possível acessar o provedor de e-mail. Tente novamente.", true); }
        catch (JsonException) { throw new FalhaIntegracaoException("resposta_invalida", "O provedor retornou uma resposta inesperada. Tente novamente.", true); }
    }
}
