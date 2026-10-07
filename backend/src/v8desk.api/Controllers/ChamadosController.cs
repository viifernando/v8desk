using Microsoft.AspNetCore.Mvc;
using v8desk.api.Contracts;
using v8desk.application.Chamados;

namespace v8desk.api.Controllers;

[Route("api/v1/chamados")]
public sealed class ChamadosController(ChamadoAplicacao aplicacao, IChamadoConsultas consultas, IProntuarioConsultas prontuario) : ApiController
{
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ChamadoDetalhe>> Obter([IdValido] Guid id, CancellationToken ct) =>
        Ok(await prontuario.ObterAsync(id, ct));

    [HttpGet("{id:guid}/mensagens")]
    public async Task<ActionResult<PaginaMensagens>> Mensagens([IdValido] Guid id,
        [FromQuery, System.ComponentModel.DataAnnotations.StringLength(4096)] string? cursor,
        CancellationToken ct, [FromQuery, System.ComponentModel.DataAnnotations.Range(1, 100)] int tamanho = 25) =>
        Ok(await prontuario.MensagensAsync(id, cursor, tamanho, ct));

    [HttpGet("{id:guid}/historico")]
    public async Task<ActionResult<PaginaEventos>> Historico([IdValido] Guid id, CancellationToken ct,
        [FromQuery, System.ComponentModel.DataAnnotations.Range(0, long.MaxValue)] long depois = 0,
        [FromQuery, System.ComponentModel.DataAnnotations.Range(1, 100)] int tamanho = 25) =>
        Ok(await prontuario.EventosAsync(id, depois, tamanho, ct));
    [HttpPost]
    [ProducesResponseType<ResultadoChamado>(201)]
    public async Task<ActionResult<ResultadoChamado>> Abrir(AbrirChamadoEntrada e, CancellationToken ct)
    {
        var resultado = await aplicacao.AbrirAsync(new(e.SetorOrigemId, e.SetorResponsavelId, e.CategoriaId,
            e.Titulo, e.Descricao, e.Prioridade, e.Visibilidade), Chave(), ct);
        return CreatedAtAction(nameof(Obter), new { id = resultado.Id }, resultado);
    }

    [HttpGet]
    public async Task<ActionResult<PaginaChamados>> Pesquisar([FromQuery] PesquisaChamadosEntrada e, CancellationToken ct) =>
        Ok(await consultas.PesquisarAsync(e.ParaFiltro(), e.Cursor, e.Tamanho, ct));

    [HttpPost("{id:guid}/aceitar")]
    public async Task<ActionResult<ResultadoChamado>> AceitarChamado([IdValido] Guid id, CancellationToken ct) =>
        Ok(await aplicacao.ExecutarAsync(new AceitarChamado(id), Chave(), ct));

    [HttpPost("{id:guid}/assumir")]
    public async Task<ActionResult<ResultadoChamado>> AssumirChamado([IdValido] Guid id, CancellationToken ct) =>
        Ok(await aplicacao.ExecutarAsync(new AssumirChamado(id), Chave(), ct));

    [HttpPost("{id:guid}/iniciar-atendimento")]
    public async Task<ActionResult<ResultadoChamado>> IniciarAtendimento([IdValido] Guid id, CancellationToken ct) =>
        Ok(await aplicacao.ExecutarAsync(new IniciarAtendimento(id), Chave(), ct));

    [HttpPost("{id:guid}/responsavel")]
    public async Task<ActionResult<ResultadoChamado>> TrocarResponsavel([IdValido] Guid id, ResponsavelEntrada e, CancellationToken ct) =>
        Ok(await aplicacao.ExecutarAsync(new TrocarResponsavel(id, e.UsuarioId, e.Motivo), Chave(), ct));

    [HttpPost("{id:guid}/solicitar-informacao")]
    public async Task<ActionResult<ResultadoChamado>> SolicitarInformacao([IdValido] Guid id, TextoEntrada e, CancellationToken ct) =>
        Ok(await aplicacao.ExecutarAsync(new SolicitarInformacao(id, e.Texto), Chave(), ct));

    [HttpPost("{id:guid}/respostas/solicitante")]
    public async Task<ActionResult<ResultadoChamado>> ResponderSolicitante([IdValido] Guid id, TextoEntrada e, CancellationToken ct) =>
        Ok(await aplicacao.ExecutarAsync(new ResponderSolicitante(id, e.Texto), Chave(), ct));

    [HttpPost("{id:guid}/respostas/equipe")]
    public async Task<ActionResult<ResultadoChamado>> ResponderEquipe([IdValido] Guid id, TextoEntrada e, CancellationToken ct) =>
        Ok(await aplicacao.ExecutarAsync(new ResponderEquipe(id, e.Texto), Chave(), ct));

    [HttpPost("{id:guid}/notas-internas")]
    public async Task<ActionResult<ResultadoChamado>> AdicionarNotaInterna([IdValido] Guid id, TextoEntrada e, CancellationToken ct) =>
        Ok(await aplicacao.ExecutarAsync(new AdicionarNotaInterna(id, e.Texto), Chave(), ct));

    [HttpPost("{id:guid}/prioridade")]
    public async Task<ActionResult<ResultadoChamado>> AlterarPrioridade([IdValido] Guid id, PrioridadeEntrada e, CancellationToken ct) =>
        Ok(await aplicacao.ExecutarAsync(new AlterarPrioridade(id, e.Prioridade, e.Motivo), Chave(), ct));

    [HttpPost("{id:guid}/categoria")]
    public async Task<ActionResult<ResultadoChamado>> AlterarCategoria([IdValido] Guid id, CategoriaEntrada e, CancellationToken ct) =>
        Ok(await aplicacao.ExecutarAsync(new AlterarCategoria(id, e.CategoriaId, e.Motivo), Chave(), ct));

    [HttpPost("{id:guid}/transferir")]
    public async Task<ActionResult<ResultadoChamado>> TransferirChamado([IdValido] Guid id, TransferenciaEntrada e, CancellationToken ct) =>
        Ok(await aplicacao.ExecutarAsync(new TransferirChamado(id, e.SetorDestinoId, e.FilaDestinoId, e.CategoriaDestinoId, e.Motivo), Chave(), ct));

    [HttpPost("{id:guid}/visibilidade")]
    public async Task<ActionResult<ResultadoChamado>> AlterarVisibilidade([IdValido] Guid id, VisibilidadeEntrada e, CancellationToken ct) =>
        Ok(await aplicacao.ExecutarAsync(new AlterarVisibilidade(id, e.Visibilidade, e.Motivo), Chave(), ct));

    [HttpPost("{id:guid}/resolver")]
    public async Task<ActionResult<ResultadoChamado>> ResolverChamado([IdValido] Guid id, TextoEntrada e, CancellationToken ct) =>
        Ok(await aplicacao.ExecutarAsync(new ResolverChamado(id, e.Texto), Chave(), ct));

    [HttpPost("{id:guid}/confirmar-solucao")]
    public async Task<ActionResult<ResultadoChamado>> ConfirmarSolucao([IdValido] Guid id, CancellationToken ct) =>
        Ok(await aplicacao.ExecutarAsync(new ConfirmarSolucao(id), Chave(), ct));

    [HttpPost("{id:guid}/rejeitar-solucao")]
    public async Task<ActionResult<ResultadoChamado>> RejeitarSolucao([IdValido] Guid id, MotivoEntrada e, CancellationToken ct) =>
        Ok(await aplicacao.ExecutarAsync(new RejeitarSolucao(id, e.Motivo), Chave(), ct));

    [HttpPost("{id:guid}/reabrir")]
    public async Task<ActionResult<ResultadoChamado>> ReabrirChamado([IdValido] Guid id, MotivoEntrada e, CancellationToken ct) =>
        Ok(await aplicacao.ExecutarAsync(new ReabrirChamado(id, e.Motivo), Chave(), ct));

    [HttpPost("{id:guid}/cancelar")]
    public async Task<ActionResult<ResultadoChamado>> CancelarChamado([IdValido] Guid id, MotivoEntrada e, CancellationToken ct) =>
        Ok(await aplicacao.ExecutarAsync(new CancelarChamado(id, e.Motivo), Chave(), ct));

    [HttpPost("{id:guid}/avaliacao")]
    public async Task<ActionResult<ResultadoChamado>> AvaliarChamado([IdValido] Guid id, AvaliacaoEntrada e, CancellationToken ct) =>
        Ok(await aplicacao.ExecutarAsync(new AvaliarChamado(id, e.Nota, e.Comentario), Chave(), ct));

    [HttpPost("{id:guid}/mensagens/{mensagemId:guid}/correcao")]
    public async Task<ActionResult<ResultadoChamado>> CorrigirMensagem([IdValido] Guid id, [IdValido] Guid mensagemId, CorrecaoMensagemEntrada e, CancellationToken ct) =>
        Ok(await aplicacao.ExecutarAsync(new CorrigirMensagem(id, mensagemId, e.Texto, e.Motivo), Chave(), ct));
}
