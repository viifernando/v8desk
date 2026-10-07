using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using v8desk.domain.Exceptions;

namespace v8desk.api.Controllers;

[ApiController]
[Authorize(Policy = "conta_empresa")]
[EnableRateLimiting("usuario")]
[RequestSizeLimit(128 * 1024)]
[ProducesResponseType<ProblemDetails>(400)]
[ProducesResponseType<ProblemDetails>(401)]
[ProducesResponseType<ProblemDetails>(403)]
[ProducesResponseType<ProblemDetails>(409)]
[ProducesResponseType<ProblemDetails>(429)]
[ProducesResponseType<ProblemDetails>(503)]
public abstract class ApiController : ControllerBase
{
    protected string Chave()
    {
        var valores = Request.Headers["Idempotency-Key"];
        var chave = valores.Count == 1 ? valores[0] : null;
        if (string.IsNullOrWhiteSpace(chave) || chave.Length > 200 || chave.Any(char.IsControl))
            throw new ValidacaoDominioException("chave_invalida", "Idempotency-Key",
                "Envie uma chave de operação no cabeçalho Idempotency-Key, com até 200 caracteres. Reutilize a mesma chave ao tentar novamente esta operação.");
        return chave;
    }
}
