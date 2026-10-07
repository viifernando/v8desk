using v8desk.domain.ValueObjects;
using v8desk.domain.Exceptions;

namespace v8desk.domain.Entities;

public sealed class IntegracoesEmpresa
{
    private IntegracoesEmpresa() { }
    public Guid Id { get; private set; }
    public Guid EmpresaId { get; private set; }
    public ConfiguracaoEmail? Email { get; private set; }
    public ConfiguracaoMicrosoft? Microsoft { get; private set; }
    public Guid? TenantMicrosoftId { get; private set; }
    public Guid? ApiMicrosoftId { get; private set; }
    public bool EmailAtivo { get; private set; }
    public bool MicrosoftAtivo { get; private set; }
    public long RevisaoEmail { get; private set; }
    public long? RevisaoEmailValidada { get; private set; }
    public DateTimeOffset? UltimoTesteEm { get; private set; }
    public string? UltimoDiagnostico { get; private set; }
    public IntegracoesEmpresa(Guid empresaId) { EmpresaId = Guarda.Identificador(empresaId, "a empresa"); Id = empresaId; }
    public void ConfigurarEmail(ConfiguracaoEmail email)
    {
        email.Validar();
        Email = email; RevisaoEmail++; RevisaoEmailValidada = null; EmailAtivo = false;
        UltimoTesteEm = null; UltimoDiagnostico = null;
    }
    public void RegistrarTeste(long revisao, bool aceito, string codigo, DateTimeOffset agora)
    {
        if (revisao != RevisaoEmail) return; // Um teste antigo nunca habilita outra configuração.
        UltimoTesteEm = agora; UltimoDiagnostico = codigo;
        RevisaoEmailValidada = aceito ? revisao : null;
    }
    public void AtivarEmail(bool ativo, DateTimeOffset agora)
    {
        if (ativo && (Email is null || RevisaoEmailValidada != RevisaoEmail))
            throw new RegraNegocioException("Envie um e-mail de teste e aguarde a confirmação antes de ativar as notificações.");
        if (ativo && Email?.CredencialExpiraEm <= agora)
            throw new RegraNegocioException("A credencial expirou. Atualize-a e teste novamente.");
        EmailAtivo = ativo;
    }
    public void ConfigurarMicrosoft(ConfiguracaoMicrosoft microsoft)
    {
        microsoft.Validar(); Microsoft = microsoft; TenantMicrosoftId = microsoft.TenantId; ApiMicrosoftId = microsoft.ApiClienteId;
        MicrosoftAtivo = false;
    }
    public void AtivarMicrosoft(bool ativo)
    {
        if (ativo && Microsoft is null) throw new RegraNegocioException("Configure a conexão Microsoft antes de ativá-la.");
        MicrosoftAtivo = ativo;
    }
}
