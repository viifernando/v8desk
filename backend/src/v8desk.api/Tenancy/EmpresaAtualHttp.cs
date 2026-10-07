using v8desk.application.Abstractions;
using v8desk.domain.Exceptions;

namespace v8desk.api.Tenancy;

public sealed class EmpresaAtualHttp(IHttpContextAccessor accessor) : IEmpresaAtual, IUsuarioAtual
{
    public Guid UsuarioId
    {
        get
        {
            var usuario = accessor.HttpContext?.User;
            var valor = usuario?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? usuario?.FindFirst("sub")?.Value;
            if (usuario?.Identity?.IsAuthenticated != true || !Guid.TryParse(valor, out var id) || id == Guid.Empty)
                throw new AcessoNegadoException("Entre com uma conta válida para continuar.");
            return id;
        }
    }
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
