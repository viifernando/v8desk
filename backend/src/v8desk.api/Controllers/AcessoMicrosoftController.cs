using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using v8desk.api.Contracts;
using v8desk.application.Integracoes;
using v8desk.api.Errors;

namespace v8desk.api.Controllers;

[ApiController, Route("api/v1/acesso/microsoft"), AllowAnonymous, EnableRateLimiting("usuario")]
public sealed class AcessoMicrosoftController(IIdentidadeMicrosoftRepository identidades) : ControllerBase
{
    [HttpGet("{empresaId:guid}")]
    public async Task<ActionResult<AcessoMicrosoft>> Configuracao([IdValido] Guid empresaId, CancellationToken ct)
    {
        var acesso = await identidades.AcessoAsync(empresaId, ct);
        if (acesso is null) return NotFound(ProblemasApi.Criar(HttpContext, 404, "Acesso Microsoft indisponível",
            "O acesso Microsoft não está habilitado para esta empresa.", "microsoft_indisponivel"));
        return Ok(acesso);
    }
}
