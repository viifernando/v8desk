using Microsoft.AspNetCore.Mvc;
using v8desk.api.Contracts;
using v8desk.application.Usuarios;

namespace v8desk.api.Controllers;

[Route("api/v1/minha-conta")]
public sealed class MinhaContaController(UsuarioAplicacao aplicacao) : ApiController
{
    [HttpPost("disponibilidade")]
    public async Task<ActionResult<ResultadoDisponibilidade>> Disponibilidade(DisponibilidadeEntrada e, CancellationToken ct) =>
        Ok(await aplicacao.DefinirDisponibilidadeAsync(new(e.Disponivel), Chave(), ct));
}
