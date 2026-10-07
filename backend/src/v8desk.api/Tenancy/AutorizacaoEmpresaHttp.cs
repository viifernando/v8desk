using v8desk.application.Abstractions;
using v8desk.domain.Exceptions;

namespace v8desk.api.Tenancy;

public sealed class AutorizacaoEmpresaHttp(IHttpContextAccessor accessor, IEmpresaAtual empresa, IUsuarioAtual usuario) : IAutorizacaoEmpresa
{
    public Task ExigirAdministracaoAsync(Guid empresaId, Guid usuarioId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var principal = accessor.HttpContext?.User;
        if (principal?.Identity?.IsAuthenticated != true || !principal.IsInRole("administrador_empresa") ||
            empresa.EmpresaId != empresaId || usuario.UsuarioId != usuarioId)
            throw new AcessoNegadoException("Esta configuração exige administração da empresa.");
        return Task.CompletedTask;
    }
}
