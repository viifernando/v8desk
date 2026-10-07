using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using v8desk.api.Contracts;
using v8desk.application.Configuracao;

namespace v8desk.api.Controllers;

[Route("api/v1/empresa")]
[Authorize(Roles = "administrador_empresa")]
public sealed class EmpresaController(ConfiguracaoEmpresaAplicacao aplicacao) : ApiController
{
    [HttpPost("nome")]
    public async Task<ActionResult<ResultadoEmpresa>> RenomearEmpresa(NomeEntrada e, CancellationToken ct) =>
        Ok(await aplicacao.ExecutarAsync(new RenomearEmpresa(e.Nome), Chave(), ct));

    [HttpPost("setores")]
    [ProducesResponseType<ResultadoEmpresa>(201)]
    public async Task<ActionResult<ResultadoEmpresa>> CriarSetor(NomeEntrada e, CancellationToken ct) =>
        StatusCode(201, await aplicacao.ExecutarAsync(new CriarSetor(e.Nome), Chave(), ct));

    [HttpPost("usuarios")]
    [ProducesResponseType<ResultadoEmpresa>(201)]
    public async Task<ActionResult<ResultadoEmpresa>> CriarUsuario(NomeEntrada e, CancellationToken ct) =>
        StatusCode(201, await aplicacao.ExecutarAsync(new CriarUsuario(e.Nome), Chave(), ct));

    [HttpPost("usuarios/{usuarioId:guid}/nome")]
    public async Task<ActionResult<ResultadoEmpresa>> RenomearUsuario([IdValido] Guid usuarioId, NomeEntrada e, CancellationToken ct) =>
        Ok(await aplicacao.ExecutarAsync(new RenomearUsuario(usuarioId, e.Nome), Chave(), ct));

    [HttpPost("usuarios/{usuarioId:guid}/desativar")]
    public async Task<ActionResult<ResultadoEmpresa>> DesativarUsuario([IdValido] Guid usuarioId, CancellationToken ct) =>
        Ok(await aplicacao.ExecutarAsync(new DesativarUsuario(usuarioId), Chave(), ct));

    [HttpPost("usuarios/{usuarioId:guid}/disponibilidade")]
    public async Task<ActionResult<ResultadoEmpresa>> DefinirDisponibilidadeUsuario([IdValido] Guid usuarioId, DisponibilidadeEntrada e, CancellationToken ct) =>
        Ok(await aplicacao.ExecutarAsync(new DefinirDisponibilidadeUsuario(usuarioId, e.Disponivel), Chave(), ct));

    [HttpPost("limite-setores-atendente")]
    public async Task<ActionResult<ResultadoEmpresa>> ConfigurarLimiteSetoresAtendente(LimiteSetoresEntrada e, CancellationToken ct) =>
        Ok(await aplicacao.ExecutarAsync(new ConfigurarLimiteSetoresAtendente(e.Limite), Chave(), ct));

    [HttpPost("ciclo-vida")]
    public async Task<ActionResult<ResultadoEmpresa>> ConfigurarCicloVidaEmpresa(CicloVidaEntrada e, CancellationToken ct) =>
        Ok(await aplicacao.ExecutarAsync(new ConfigurarCicloVidaEmpresa(e.ParaDominio()), Chave(), ct));

    [HttpPost("calendario/fuso")]
    public async Task<ActionResult<ResultadoEmpresa>> AlterarFusoHorario(FusoEntrada e, CancellationToken ct) =>
        Ok(await aplicacao.ExecutarAsync(new AlterarFusoHorario(e.FusoHorarioId), Chave(), ct));

    [HttpPost("calendario/expediente")]
    public async Task<ActionResult<ResultadoEmpresa>> DefinirExpediente(ExpedienteEntrada e, CancellationToken ct) =>
        Ok(await aplicacao.ExecutarAsync(new DefinirExpediente(e.Dia, e.Intervalos.Select(i => new IntervaloExpedienteEntrada(i.Inicio, i.Fim)).ToArray()), Chave(), ct));

    [HttpPost("calendario/excecoes")]
    public async Task<ActionResult<ResultadoEmpresa>> RegistrarExcecaoCalendario(ExcecaoCalendarioEntrada e, CancellationToken ct) =>
        Ok(await aplicacao.ExecutarAsync(new RegistrarExcecaoCalendario(e.Data, e.Motivo, e.Intervalos.Select(i => new IntervaloExpedienteEntrada(i.Inicio, i.Fim)).ToArray()), Chave(), ct));

    [HttpPost("calendario/excecoes/{data}/remover")]
    public async Task<ActionResult<ResultadoEmpresa>> RemoverExcecaoCalendario(DateOnly data, CancellationToken ct) =>
        Ok(await aplicacao.ExecutarAsync(new RemoverExcecaoCalendario(data), Chave(), ct));

}
