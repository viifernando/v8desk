using System.Net.Mail;
using v8desk.domain.Exceptions;

namespace v8desk.domain.ValueObjects;

public enum ProvedorEmail { Smtp, MicrosoftGraph }
public enum SegurancaSmtp { StartTls, TlsImplicito }
public sealed record ConfiguracaoEmail
{
    public ProvedorEmail Provedor { get; init; }
    public string Remetente { get; init; } = "";
    public string NomeRemetente { get; init; } = "";
    public string? HostSmtp { get; init; }
    public int PortaSmtp { get; init; } = 587;
    public SegurancaSmtp SegurancaSmtp { get; init; }
    public string? UsuarioSmtp { get; init; }
    public string? CredencialProtegida { get; init; }
    public Guid? TenantGraphId { get; init; }
    public Guid? ClienteGraphId { get; init; }
    public DateTimeOffset? CredencialExpiraEm { get; init; }
    public void Validar()
    {
        EmailValido(Remetente);
        if (!Enum.IsDefined(Provedor) || !Enum.IsDefined(SegurancaSmtp))
            throw new ValidacaoDominioException("provedor_invalido", "provedor", "Escolha um provedor e segurança válidos.");
        if (string.IsNullOrWhiteSpace(NomeRemetente) || NomeRemetente.Length > 200 || NomeRemetente.Any(char.IsControl))
            throw new ValidacaoDominioException("nome_invalido", "nomeRemetente", "Informe um nome de remetente válido, com até 200 caracteres.");
        if (Provedor == ProvedorEmail.Smtp)
        {
            if (string.IsNullOrWhiteSpace(HostSmtp) || HostSmtp.Length > 253 || HostSmtp.Contains('/') || HostSmtp.Any(char.IsWhiteSpace) || PortaSmtp is < 1 or > 65535)
                throw new ValidacaoDominioException("smtp_invalido", "hostSmtp", "Informe o servidor SMTP e uma porta válida.");
            if (!string.IsNullOrEmpty(UsuarioSmtp) && string.IsNullOrEmpty(CredencialProtegida))
                throw new ValidacaoDominioException("credencial_obrigatoria", "segredo", "Informe a credencial do servidor SMTP.");
        }
        else if (TenantGraphId is null || TenantGraphId == Guid.Empty || ClienteGraphId is null || ClienteGraphId == Guid.Empty || string.IsNullOrEmpty(CredencialProtegida))
            throw new ValidacaoDominioException("graph_invalido", "graph", "Informe o tenant, o aplicativo e a credencial do Microsoft Graph.");
    }
    public static string EmailValido(string email)
    {
        if (string.IsNullOrWhiteSpace(email) || email.Length > 254 || email.Any(char.IsWhiteSpace) || !MailAddress.TryCreate(email, out var endereco)
            || endereco.Address != email || !email.Contains('@') || email.Any(char.IsControl))
            throw new ValidacaoDominioException("email_invalido", "email", "Informe um endereço de e-mail válido.");
        return email;
    }
}
public sealed record ConfiguracaoMicrosoft(Guid TenantId, Guid ApiClienteId, Guid ClienteLoginId)
{
    public void Validar()
    {
        if (TenantId == Guid.Empty || ApiClienteId == Guid.Empty || ClienteLoginId == Guid.Empty)
            throw new ValidacaoDominioException("microsoft_invalido", "microsoft", "Informe os identificadores do tenant, da API e do aplicativo de login.");
    }
}
