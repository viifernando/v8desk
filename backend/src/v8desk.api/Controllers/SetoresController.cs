using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using v8desk.api.Contracts;
using v8desk.application.Configuracao;

namespace v8desk.api.Controllers;

[Route("api/v1/setores/{setorId:guid}")]
public sealed class SetoresController(ConfiguracaoSetorAplicacao aplicacao) : ApiController
{
    [HttpPost("nome")]
    public async Task<ActionResult<ResultadoConfiguracao>> RenomearSetor([IdValido] Guid setorId, NomeEntrada e, CancellationToken ct) =>
        Ok(await aplicacao.ExecutarAsync(new RenomearSetor(setorId, e.Nome), Chave(), ct));

    [HttpPost("desativar")]
    public async Task<ActionResult<ResultadoConfiguracao>> DesativarSetor([IdValido] Guid setorId, CancellationToken ct) =>
        Ok(await aplicacao.ExecutarAsync(new DesativarSetor(setorId), Chave(), ct));

    [HttpPost("filas")]
    public async Task<ActionResult<ResultadoConfiguracao>> CriarFila([IdValido] Guid setorId, NomeEntrada e, CancellationToken ct) =>
        Ok(await aplicacao.ExecutarAsync(new CriarFila(setorId, e.Nome), Chave(), ct));

    [HttpPost("filas/{filaId:guid}/nome")]
    public async Task<ActionResult<ResultadoConfiguracao>> RenomearFila([IdValido] Guid setorId, [IdValido] Guid filaId, NomeEntrada e, CancellationToken ct) =>
        Ok(await aplicacao.ExecutarAsync(new RenomearFila(setorId, filaId, e.Nome), Chave(), ct));

    [HttpPost("filas/{filaId:guid}/desativar")]
    public async Task<ActionResult<ResultadoConfiguracao>> DesativarFila([IdValido] Guid setorId, [IdValido] Guid filaId, CancellationToken ct) =>
        Ok(await aplicacao.ExecutarAsync(new DesativarFila(setorId, filaId), Chave(), ct));

    [HttpPost("categorias")]
    public async Task<ActionResult<ResultadoConfiguracao>> CriarCategoria([IdValido] Guid setorId, NovaCategoriaEntrada e, CancellationToken ct) =>
        Ok(await aplicacao.ExecutarAsync(new CriarCategoria(setorId, e.Nome, e.PaiId), Chave(), ct));

    [HttpPost("categorias/{categoriaId:guid}/nome")]
    public async Task<ActionResult<ResultadoConfiguracao>> RenomearCategoria([IdValido] Guid setorId, [IdValido] Guid categoriaId, NomeEntrada e, CancellationToken ct) =>
        Ok(await aplicacao.ExecutarAsync(new RenomearCategoria(setorId, categoriaId, e.Nome), Chave(), ct));

    [HttpPost("categorias/{categoriaId:guid}/pai")]
    public async Task<ActionResult<ResultadoConfiguracao>> MoverCategoria([IdValido] Guid setorId, [IdValido] Guid categoriaId, PaiCategoriaEntrada e, CancellationToken ct) =>
        Ok(await aplicacao.ExecutarAsync(new MoverCategoria(setorId, categoriaId, e.PaiId), Chave(), ct));

    [HttpPost("categorias/{categoriaId:guid}/desativar")]
    public async Task<ActionResult<ResultadoConfiguracao>> DesativarCategoria([IdValido] Guid setorId, [IdValido] Guid categoriaId, CancellationToken ct) =>
        Ok(await aplicacao.ExecutarAsync(new DesativarCategoria(setorId, categoriaId), Chave(), ct));

    [HttpPost("categorias/{categoriaId:guid}/reativar")]
    public async Task<ActionResult<ResultadoConfiguracao>> ReativarCategoria([IdValido] Guid setorId, [IdValido] Guid categoriaId, CancellationToken ct) =>
        Ok(await aplicacao.ExecutarAsync(new ReativarCategoria(setorId, categoriaId), Chave(), ct));

    [HttpPost("categorias/{categoriaId:guid}/recebimento")]
    public async Task<ActionResult<ResultadoConfiguracao>> ConfigurarRecebimentoCategoria([IdValido] Guid setorId, [IdValido] Guid categoriaId, RecebimentoCategoriaEntrada e, CancellationToken ct) =>
        Ok(await aplicacao.ExecutarAsync(new ConfigurarRecebimentoCategoria(setorId, categoriaId, e.PermitirComSubcategorias), Chave(), ct));

    [HttpPost("categorias/{categoriaId:guid}/destino")]
    public async Task<ActionResult<ResultadoConfiguracao>> ConfigurarDestinoCategoria([IdValido] Guid setorId, [IdValido] Guid categoriaId, DestinoCategoriaEntrada e, CancellationToken ct) =>
        Ok(await aplicacao.ExecutarAsync(new ConfigurarDestinoCategoria(setorId, categoriaId, e.FilaId), Chave(), ct));

    [HttpPost("categorias/{categoriaId:guid}/visibilidade")]
    public async Task<ActionResult<ResultadoConfiguracao>> ConfigurarVisibilidadeCategoria([IdValido] Guid setorId, [IdValido] Guid categoriaId, VisibilidadeCategoriaEntrada e, CancellationToken ct) =>
        Ok(await aplicacao.ExecutarAsync(new ConfigurarVisibilidadeCategoria(setorId, categoriaId, e.Visibilidade), Chave(), ct));

    [HttpPost("categorias/{categoriaId:guid}/pre-qualificados/{usuarioId:guid}")]
    public async Task<ActionResult<ResultadoConfiguracao>> PreQualificarCategoria([IdValido] Guid setorId, [IdValido] Guid categoriaId, [IdValido] Guid usuarioId, CancellationToken ct) =>
        Ok(await aplicacao.ExecutarAsync(new PreQualificarCategoria(setorId, categoriaId, usuarioId), Chave(), ct));

    [HttpPost("categorias/{categoriaId:guid}/pre-qualificados/{usuarioId:guid}/remover")]
    public async Task<ActionResult<ResultadoConfiguracao>> RemoverPreQualificacao([IdValido] Guid setorId, [IdValido] Guid categoriaId, [IdValido] Guid usuarioId, CancellationToken ct) =>
        Ok(await aplicacao.ExecutarAsync(new RemoverPreQualificacao(setorId, categoriaId, usuarioId), Chave(), ct));

    [HttpPost("sla")]
    public async Task<ActionResult<ResultadoConfiguracao>> DefinirMetaSla([IdValido] Guid setorId, MetaSlaEntrada e, CancellationToken ct) =>
        Ok(await aplicacao.ExecutarAsync(new DefinirMetaSla(setorId, e.Prioridade, e.Tipo, e.HorasUteis), Chave(), ct));

    [HttpPost("ciclo-vida")]
    public async Task<ActionResult<ResultadoConfiguracao>> ConfigurarCicloVidaSetor([IdValido] Guid setorId, CicloVidaEntrada e, CancellationToken ct) =>
        Ok(await aplicacao.ExecutarAsync(new ConfigurarCicloVidaSetor(setorId, e.ParaDominio()), Chave(), ct));

    [HttpPost("ciclo-vida/herdar")]
    public async Task<ActionResult<ResultadoConfiguracao>> HerdarCicloVida([IdValido] Guid setorId, CancellationToken ct) =>
        Ok(await aplicacao.ExecutarAsync(new ConfigurarCicloVidaSetor(setorId, null), Chave(), ct));

    [HttpPost("filas/{filaId:guid}/acesso-restrito")]
    public async Task<ActionResult<ResultadoConfiguracao>> ConfigurarAcessoRestritoFila([IdValido] Guid setorId, [IdValido] Guid filaId, AcessoRestritoEntrada e, CancellationToken ct) =>
        Ok(await aplicacao.ExecutarAsync(new ConfigurarAcessoRestritoFila(setorId, filaId, e.Regra), Chave(), ct));

    [HttpPost("filas/{filaId:guid}/restritos/{usuarioId:guid}")]
    public async Task<ActionResult<ResultadoConfiguracao>> AutorizarRestritos([IdValido] Guid setorId, [IdValido] Guid filaId, [IdValido] Guid usuarioId, CancellationToken ct) =>
        Ok(await aplicacao.ExecutarAsync(new AutorizarRestritos(setorId, filaId, usuarioId), Chave(), ct));

    [HttpPost("filas/{filaId:guid}/restritos/{usuarioId:guid}/revogar")]
    public async Task<ActionResult<ResultadoConfiguracao>> RevogarRestritos([IdValido] Guid setorId, [IdValido] Guid filaId, [IdValido] Guid usuarioId, CancellationToken ct) =>
        Ok(await aplicacao.ExecutarAsync(new RevogarRestritos(setorId, filaId, usuarioId), Chave(), ct));

    [HttpPost("filas/{filaId:guid}/membros/{vinculoId:guid}")]
    public async Task<ActionResult<ResultadoConfiguracao>> AdicionarMembroFila([IdValido] Guid setorId, [IdValido] Guid filaId, [IdValido] Guid vinculoId, CancellationToken ct) =>
        Ok(await aplicacao.ExecutarAsync(new AdicionarMembroFila(setorId, filaId, vinculoId), Chave(), ct));

    [HttpPost("filas/{filaId:guid}/membros/{usuarioId:guid}/remover")]
    public async Task<ActionResult<ResultadoConfiguracao>> RemoverMembroFila([IdValido] Guid setorId, [IdValido] Guid filaId, [IdValido] Guid usuarioId, CancellationToken ct) =>
        Ok(await aplicacao.ExecutarAsync(new RemoverMembroFila(setorId, filaId, usuarioId), Chave(), ct));

    [HttpPost("vinculos")]
    public async Task<ActionResult<ResultadoConfiguracao>> CriarVinculoSetor([IdValido] Guid setorId, VinculoEntrada e, CancellationToken ct) =>
        Ok(await aplicacao.ExecutarAsync(new CriarVinculoSetor(setorId, e.UsuarioId, e.Papel), Chave(), ct));

    [HttpPost("vinculos/{vinculoId:guid}/papeis/conceder")]
    public async Task<ActionResult<ResultadoConfiguracao>> ConcederPapelSetor([IdValido] Guid setorId, [IdValido] Guid vinculoId, PapelEntrada e, CancellationToken ct) =>
        Ok(await aplicacao.ExecutarAsync(new ConcederPapelSetor(setorId, vinculoId, e.Papel), Chave(), ct));

    [HttpPost("vinculos/{vinculoId:guid}/papeis/revogar")]
    public async Task<ActionResult<ResultadoConfiguracao>> RevogarPapelSetor([IdValido] Guid setorId, [IdValido] Guid vinculoId, PapelEntrada e, CancellationToken ct) =>
        Ok(await aplicacao.ExecutarAsync(new RevogarPapelSetor(setorId, vinculoId, e.Papel), Chave(), ct));

    [HttpPost("vinculos/{vinculoId:guid}/desativar")]
    public async Task<ActionResult<ResultadoConfiguracao>> DesativarVinculoSetor([IdValido] Guid setorId, [IdValido] Guid vinculoId, CancellationToken ct) =>
        Ok(await aplicacao.ExecutarAsync(new DesativarVinculoSetor(setorId, vinculoId), Chave(), ct));

}
