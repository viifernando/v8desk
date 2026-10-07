using v8desk.application.Abstractions;
using v8desk.domain.Exceptions;

namespace v8desk.api.Tenancy;

public sealed class EmpresaAtualHttp(IHttpContextAccessor accessor) : IEmpresaAtual
{
    public Guid EmpresaId
    {
        get
        {
            var usuario = accessor.HttpContext?.User;
            if (usuario?.Identity?.IsAuthenticated != true ||
                !Guid.TryParse(usuario.FindFirst("empresa_id")?.Value, out var id) || id == Guid.Empty)
                throw new AcessoNegadoException("Entre com uma conta vinculada à empresa para continuar.");
            return id;
        }
    }
}
