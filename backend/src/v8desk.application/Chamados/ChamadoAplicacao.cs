using v8desk.application.Abstractions;
using v8desk.application.Common;
using v8desk.application.Seguranca;
using v8desk.domain.Entities;
using v8desk.domain.Exceptions;
using v8desk.domain.ValueObjects;

namespace v8desk.application.Chamados;

public sealed class ChamadoAplicacao(IChamadoRepository chamados, IConfiguracaoRepository configuracoes,
    IAcessoRepository acessos, IExecutorComandoIdempotente executor, IUnidadeTrabalho unidade,
    IEmpresaAtual empresa, IUsuarioAtual usuario, TimeProvider relogio)
{
    public async Task<ResultadoChamado> AbrirAsync(AbrirChamado comando, string chave, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(comando);
        await ExigirAberturaAsync(comando, cancellationToken); // Também valida a autorização em replays.
        return await ComandoIdempotente.ExecutarAsync(executor, chave, comando, async ct =>
        {
            await ExigirAberturaAsync(comando, ct);
            var setor = await SetorAsync(comando.SetorResponsavelId, ct);
            var categoria = Categoria(setor, comando.CategoriaId);
            var fila = setor.Filas.Single(f => f.Id == setor.DeterminarFilaInicial(categoria));
            var origem = comando.SetorOrigemId == setor.Id ? setor : await SetorAsync(comando.SetorOrigemId, ct);
            var config = await ConfiguracaoAsync(setor, ct);
            var chamado = Chamado.Abrir(new(empresa.EmpresaId, usuario.UsuarioId, new(origem.Id, origem.Nome), fila,
                categoria, comando.Titulo, comando.Descricao, comando.Prioridade, comando.Visibilidade),
                new(usuario.UsuarioId, relogio.GetUtcNow()), config);
            chamados.Adicionar(chamado);
            await unidade.SalvarAsync(ct); // Obtém o número gerado antes de persistir o resultado idempotente.
            return Resultado(chamado);
        }, cancellationToken);
    }

    public async Task<ResultadoChamado> ExecutarAsync(ComandoChamado comando, string chave, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(comando);
        await AutorizarAsync(comando, await ReferenciaAsync(comando.ChamadoId, cancellationToken), cancellationToken);
        return await ComandoIdempotente.ExecutarAsync(executor, chave, comando, async ct =>
        {
            var chamado = await chamados.ObterParaAtualizacaoAsync(comando.ChamadoId, ct)
                ?? throw Indisponivel();
            var acesso = await AutorizarAsync(comando, AutorizacaoChamado.Referencia(chamado), ct);
            var contexto = new ContextoOperacao(usuario.UsuarioId, relogio.GetUtcNow());
            var fila = acesso.Fila(chamado.FilaAtualId);
            switch (comando)
            {
                case AceitarChamado: chamado.Aceitar(contexto, acesso); break;
                case AssumirChamado: chamado.Assumir(acesso.Usuario, fila, contexto); break;
                case IniciarAtendimento: chamado.IniciarAtendimento(contexto, acesso); break;
                case TrocarResponsavel c:
                    var destino = c.UsuarioId is { } id ? await acessos.ObterUsuarioAsync(id, ct)
                        ?? throw new RegraNegocioException("O atendente selecionado não está disponível.") : null;
                    chamado.TrocarResponsavel(destino, fila, c.Motivo, contexto, acesso); break;
                case SolicitarInformacao c: chamado.SolicitarInformacao(c.Pergunta, contexto, acesso); break;
                case ResponderSolicitante c: chamado.ResponderSolicitante(c.Texto, contexto); break;
                case ResponderEquipe c: chamado.ResponderEquipe(c.Texto, contexto, acesso); break;
                case AdicionarNotaInterna c: chamado.AdicionarNotaInterna(c.Texto, contexto, acesso); break;
                case AlterarPrioridade c:
                    chamado.AlterarPrioridade(c.Prioridade, c.Motivo, (await SetorAsync(chamado.SetorAtualId, ct)).Sla, contexto, acesso); break;
                case AlterarCategoria c:
                    chamado.AlterarCategoria(Categoria(await SetorAsync(chamado.SetorAtualId, ct), c.CategoriaId), fila, c.Motivo, contexto, acesso); break;
                case TransferirChamado c:
                    var setor = await SetorAsync(c.SetorDestinoId, ct);
                    chamado.Transferir(acesso.Fila(c.FilaDestinoId), Categoria(setor, c.CategoriaDestinoId), c.Motivo,
                        await ConfiguracaoAsync(setor, ct), contexto, acesso); break;
                case AlterarVisibilidade c: chamado.AlterarVisibilidade(c.Visibilidade, c.Motivo, fila, contexto, acesso); break;
                case ResolverChamado c:
                    chamado.Resolver(c.Solucao, await ConfiguracaoAtualAsync(chamado, ct), contexto, acesso); break;
                case ConfirmarSolucao: chamado.ConfirmarSolucao(contexto); break;
                case RejeitarSolucao c:
                    chamado.InformarSolucaoInsuficiente(c.Motivo, contexto, await ConfiguracaoAtualAsync(chamado, ct)); break;
                case ReabrirChamado c:
                    var responsavel = chamado.ResponsavelId is { } responsavelId ? await acessos.ObterUsuarioAsync(responsavelId, ct) : null;
                    chamado.Reabrir(c.Motivo, fila, responsavel, await ConfiguracaoAtualAsync(chamado, ct), contexto); break;
                case CancelarChamado c: chamado.Cancelar(c.Motivo, contexto, acesso); break;
                case AvaliarChamado c: chamado.Avaliar(c.Nota, c.Comentario, contexto); break;
                case CorrigirMensagem c: chamado.CorrigirMensagem(c.MensagemId, c.Texto, c.Motivo, contexto); break;
                default: throw new ValidacaoDominioException("comando_invalido", "comando", "Esta operação não é reconhecida.");
            }
            await unidade.SalvarAsync(ct);
            return Resultado(chamado);
        }, cancellationToken);
    }

    private async Task ExigirAberturaAsync(AbrirChamado comando, CancellationToken ct)
    {
        var acesso = await AcessoAsync([], ct);
        if (!acesso.PossuiVinculo(comando.SetorOrigemId))
            throw new AcessoNegadoException("Escolha um setor de origem ao qual você está vinculado.");
        if (!acesso.SetorDisponivel(comando.SetorResponsavelId))
            throw new RegraNegocioException("Escolha um setor responsável ativo.");
    }
    private async Task<AutorizacaoChamado> AutorizarAsync(ComandoChamado comando, ReferenciaAcessoChamado referencia, CancellationToken ct)
    {
        var filas = comando is TransferirChamado t ? new[] { referencia.FilaAtualId, t.FilaDestinoId } : [referencia.FilaAtualId];
        var acesso = await AcessoAsync(filas, ct);
        switch (comando)
        {
            case ResponderSolicitante or ConfirmarSolucao or RejeitarSolucao or ReabrirChamado or AvaliarChamado:
                acesso.ExigirSolicitante(referencia); break;
            case CancelarChamado: acesso.ExigirCancelamento(referencia); break;
            case CorrigirMensagem: acesso.ExigirVisualizacao(referencia); break; // Autoria verificada pelo domínio.
            default: acesso.ExigirAtendimento(referencia); break;
        }
        return acesso;
    }
    private async Task<AutorizacaoChamado> AcessoAsync(IReadOnlyCollection<Guid> filas, CancellationToken ct)
    {
        var dados = await acessos.ObterDadosAsync(usuario.UsuarioId, filas, ct) ?? throw Indisponivel();
        if (dados.Usuario.Id != usuario.UsuarioId || dados.Usuario.EmpresaId != empresa.EmpresaId || !dados.Usuario.Ativo) throw Indisponivel();
        return new(empresa.EmpresaId, dados);
    }
    private async Task<ReferenciaAcessoChamado> ReferenciaAsync(Guid id, CancellationToken ct)
    {
        if (id == Guid.Empty) throw new ValidacaoDominioException("identificador_invalido", "chamadoId", "Informe um chamado válido.");
        return await acessos.ObterReferenciaAsync(id, ct) ?? throw Indisponivel();
    }
    private async Task<Setor> SetorAsync(Guid id, CancellationToken ct)
    {
        var setor = await configuracoes.ObterSetorAsync(id, ct) ?? throw new RegraNegocioException("O setor selecionado não está disponível.");
        if (setor.EmpresaId != empresa.EmpresaId || !setor.Ativo) throw new AcessoNegadoException("O setor não está ativo nesta empresa.");
        return setor;
    }
    private static Categoria Categoria(Setor setor, Guid id) => setor.Categorias.SingleOrDefault(c => c.Id == id)
        ?? throw new RegraNegocioException("Selecione uma categoria do setor responsável.");
    private async Task<ConfiguracaoAplicada> ConfiguracaoAsync(Setor setor, CancellationToken ct)
    {
        var atual = await configuracoes.ObterEmpresaAsync(ct) ?? throw Indisponivel();
        if (atual.Id != empresa.EmpresaId) throw Indisponivel();
        return new(new(setor.Id, setor.Nome), setor.Sla, setor.ObterCicloVida(atual.PadraoCicloVida), atual.Calendario);
    }
    private async Task<ConfiguracaoAplicada> ConfiguracaoAtualAsync(Chamado chamado, CancellationToken ct) =>
        await ConfiguracaoAsync(await SetorAsync(chamado.SetorAtualId, ct), ct);
    private static ResultadoChamado Resultado(Chamado c) => new(c.Id, c.Numero, c.Status, c.Versao);
    private static AcessoNegadoException Indisponivel() => new("O chamado ou os dados necessários não estão disponíveis para sua conta.");
}
