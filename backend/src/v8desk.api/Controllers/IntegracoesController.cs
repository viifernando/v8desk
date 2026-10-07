using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using v8desk.api.Contracts;
using v8desk.application.Integracoes;

namespace v8desk.api.Controllers;

[Route("api/v1/integracoes")]
[Authorize(Roles = "administrador_empresa")]
public sealed class IntegracoesController(IntegracoesAplicacao aplicacao, IConfiguration config) : ApiController
{
    [HttpGet]
    public async Task<ActionResult<ResumoIntegracoes>> Obter(CancellationToken ct) =>
        Ok((await aplicacao.ObterAsync(ct)) with { ProcessamentoAtivo = config.GetValue("Integrations:WorkerEnabled", false) });
    [HttpPost("email")]
    public async Task<ActionResult<ResultadoIntegracao>> ConfigurarEmail(EmailIntegracaoEntrada e, CancellationToken ct) =>
        Ok(await aplicacao.ExecutarAsync(e.ParaComando(), Chave(), ct));
    [HttpPost("email/ativacao")]
    public async Task<ActionResult<ResultadoIntegracao>> AtivarEmail(AtivacaoIntegracaoEntrada e, CancellationToken ct) =>
        Ok(await aplicacao.ExecutarAsync(new AtivarEmail(e.Ativo), Chave(), ct));
    [HttpPost("email/diagnostico")]
    public async Task<ActionResult<DiagnosticoIntegracao>> Diagnosticar(CancellationToken ct) =>
        Ok(await aplicacao.DiagnosticarAsync(ct));
    [HttpPost("email/testar")]
    [ProducesResponseType<ResultadoIntegracao>(202)]
    public async Task<ActionResult<ResultadoIntegracao>> Testar(CancellationToken ct)
    {
        var resultado = await aplicacao.ExecutarAsync(new TestarEmail(), Chave(), ct);
        if (!config.GetValue("Integrations:WorkerEnabled", false))
            resultado = resultado with { Mensagem = "Teste agendado. O serviço de envio está pausado no servidor; solicite sua ativação ao administrador da instalação." };
        return Accepted("/api/v1/integracoes/email/entregas", resultado);
    }
    [HttpGet("email/entregas")]
    public async Task<ActionResult<IReadOnlyList<EntregaResumo>>> Entregas(CancellationToken ct, [FromQuery, Range(1, 100)] int tamanho = 25) =>
        Ok(await aplicacao.EntregasAsync(tamanho, ct));
    [HttpPost("email/entregas/{id:guid}/repetir")]
    public async Task<ActionResult<ResultadoIntegracao>> Repetir([IdValido] Guid id, CancellationToken ct) =>
        Ok(await aplicacao.ExecutarAsync(new RepetirEntrega(id), Chave(), ct));
    [HttpPost("microsoft")]
    public async Task<ActionResult<ResultadoIntegracao>> ConfigurarMicrosoft(MicrosoftIntegracaoEntrada e, CancellationToken ct) =>
        Ok(await aplicacao.ExecutarAsync(new ConfigurarMicrosoft(e.TenantId, e.ApiClienteId, e.ClienteLoginId), Chave(), ct));
    [HttpPost("microsoft/ativacao")]
    public async Task<ActionResult<ResultadoIntegracao>> AtivarMicrosoft(AtivacaoIntegracaoEntrada e, CancellationToken ct) =>
        Ok(await aplicacao.ExecutarAsync(new AtivarMicrosoft(e.Ativo), Chave(), ct));
    [HttpPost("usuarios/{usuarioId:guid}/microsoft")]
    public async Task<ActionResult<ResultadoIntegracao>> Vincular([IdValido] Guid usuarioId, VinculoMicrosoftEntrada e, CancellationToken ct) =>
        Ok(await aplicacao.ExecutarAsync(new VincularMicrosoft(usuarioId, e.ObjetoId, e.Administrador), Chave(), ct));
    [HttpPost("usuarios/{usuarioId:guid}/microsoft/desativar")]
    public async Task<ActionResult<ResultadoIntegracao>> Desvincular([IdValido] Guid usuarioId, DesvinculoMicrosoftEntrada e, CancellationToken ct) =>
        Ok(await aplicacao.ExecutarAsync(new DesvincularMicrosoft(usuarioId, e.ObjetoId), Chave(), ct));
    [HttpPost("usuarios/{usuarioId:guid}/email")]
    public async Task<ActionResult<ResultadoIntegracao>> Contato([IdValido] Guid usuarioId, ContatoNotificacaoEntrada e, CancellationToken ct) =>
        Ok(await aplicacao.ExecutarAsync(new ConfigurarContato(usuarioId, e.Email), Chave(), ct));
}
