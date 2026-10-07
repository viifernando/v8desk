using Microsoft.AspNetCore.DataProtection;
using v8desk.application.Integracoes;

namespace v8desk.api.Integracoes;

public sealed class ProtecaoCredencial(IDataProtectionProvider provider) : IProtecaoCredencial
{
    private IDataProtector Protetor(Guid empresaId) => provider.CreateProtector("V8Desk", "Integracoes", "Credencial", "v1", empresaId.ToString());
    public string Proteger(Guid empresaId, string segredo) => Protetor(empresaId).Protect(segredo);
    public string Revelar(Guid empresaId, string protegido) => Protetor(empresaId).Unprotect(protegido);
}
