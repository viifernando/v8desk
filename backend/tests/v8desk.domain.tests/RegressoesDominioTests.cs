using v8desk.domain.Entities;
using v8desk.domain.Enums;
using v8desk.domain.Exceptions;
using v8desk.domain.Interfaces;
using v8desk.domain.ValueObjects;

namespace v8desk.domain.tests;

public class RegressoesDominioTests
{
    private static readonly DateTimeOffset Inicio = new(2026, 10, 5, 8, 0, 0, TimeSpan.Zero);
    private static readonly Permitir Acesso = new();

    public void EntradasInvalidasTemMensagensClarasECodigos()
    {
        var erros = new Action[]
        {
            () => new HorasUteis(0),
            () => new HorasUteis(decimal.MaxValue),
            () => new Avaliacao(6, null, Guid.NewGuid(), Inicio),
            () => new ContextoOperacao(Guid.Empty, Inicio),
            () => new ContextoOperacao(Guid.NewGuid(), default),
            () => new IntervaloExpediente(new(17, 0), new(8, 0)),
            () => new PoliticaCicloVida(new(32), TimeSpan.Zero, 1, TimeSpan.FromDays(7)),
            () => new Avaliacao(5, new string('a', 2001), Guid.NewGuid(), Inicio)
        };
        foreach (var acao in erros)
        {
            try { acao(); throw new Exception("Deveria rejeitar a entrada."); }
            catch (ValidacaoDominioException erro)
            {
                Assert.False(string.IsNullOrWhiteSpace(erro.Codigo));
                Assert.False(string.IsNullOrWhiteSpace(erro.Campo));
                Assert.False(erro.Message.Contains("Parameter"));
            }
        }
        Assert.Equal("Comentário", new Avaliacao(5, "  Comentário  ", Guid.NewGuid(), Inicio).Comentario);
        Assert.Equal<string?>(null, new Avaliacao(5, "  ", Guid.NewGuid(), Inicio).Comentario);
    }

    public void AcaoRetroativaNaoDeixaMutacaoParcial()
    {
        var f = new Cenario();
        f.Chamado.SolicitarInformacao("Detalhes?", f.Contexto(Inicio.AddHours(2)), Acesso);
        var versao = f.Chamado.Versao;
        var mensagens = f.Chamado.Mensagens.Count;
        Assert.Throws<ValidacaoDominioException>(() => f.Chamado.ResponderSolicitante("Resposta", f.Contexto(Inicio.AddHours(1))));
        Assert.Equal(versao, f.Chamado.Versao);
        Assert.Equal(mensagens, f.Chamado.Mensagens.Count);
        Assert.Equal(StatusChamado.AguardandoInformacao, f.Chamado.Status);
        Assert.True(f.Chamado.CicloAtual.SlaAtivo(TipoSla.Resolucao)!.Pausado);
    }

    public void AberturaRejeitaTextosExcessivosEFilaDeOutroSetor()
    {
        var f = new Cenario();
        var dados = new DadosAbertura(f.Empresa.Id, f.Usuario.Id, new(f.Setor.Id, f.Setor.Nome),
            f.Setor.FilaGeral, f.Categoria, "Título", "Descrição", Prioridade.Media, Visibilidade.CompartilhadoComSetor);
        Assert.Throws<ValidacaoDominioException>(() => Chamado.Abrir(dados with { Titulo = new string('x', 201) }, f.Contexto(Inicio), f.Config(f.Setor)));
        Assert.Throws<ValidacaoDominioException>(() => Chamado.Abrir(dados with { Prioridade = (Prioridade)99 }, f.Contexto(Inicio), f.Config(f.Setor)));
        var outro = f.Empresa.CriarSetor("RH");
        Assert.Throws<RegraNegocioException>(() => Chamado.Abrir(dados with { Fila = outro.FilaGeral }, f.Contexto(Inicio), f.Config(f.Setor)));
    }

    public void CategoriaRestritaNaoPodeSerAbertaCompartilhada()
    {
        var f = new Cenario();
        f.Categoria.DefinirVisibilidadePadrao(Visibilidade.AcessoRestrito);
        var dados = new DadosAbertura(f.Empresa.Id, f.Usuario.Id, new(f.Setor.Id, f.Setor.Nome),
            f.Setor.FilaGeral, f.Categoria, "Título", "Descrição", Prioridade.Media, Visibilidade.CompartilhadoComSetor);
        Assert.Throws<RegraNegocioException>(() => Chamado.Abrir(dados, f.Contexto(Inicio), f.Config(f.Setor)));
        var vinculo = new VinculoSetor(f.Usuario, f.Setor.Id, PapelSetor.Atendente);
        f.Setor.FilaGeral.AdicionarMembro(vinculo);
        f.Setor.FilaGeral.AutorizarAcessoRestrito(f.Usuario.Id);
        var chamado = Chamado.Abrir(dados, f.Contexto(Inicio), f.Config(f.Setor));
        Assert.Equal(Visibilidade.AcessoRestrito, chamado.Visibilidade);
    }

    public void UsuariosDeOutraEmpresaNaoPodemIntegrarFilaOuPreQualificacao()
    {
        var f = new Cenario();
        var externo = new Usuario(Guid.NewGuid(), "Externo");
        Assert.Throws<RegraNegocioException>(() => f.Categoria.PreQualificar(externo, Inicio));
        Assert.Throws<RegraNegocioException>(() => f.Setor.FilaGeral.AdicionarMembro(new(externo, f.Setor.Id, PapelSetor.Atendente)));
    }

    public void HierarquiaRejeitaCiclosEOutrosSetoresSemModificarArvore()
    {
        var f = new Cenario();
        var filha = f.Setor.CriarCategoria("ERP", f.Categoria);
        var neta = f.Setor.CriarCategoria("Acesso", filha);
        Assert.Throws<RegraNegocioException>(() => f.Setor.MoverCategoria(f.Categoria, neta));
        Assert.Equal<Guid?>(null, f.Categoria.CategoriaPaiId);
        Assert.Equal(f.Categoria.Id, filha.CategoriaPaiId);
        var outro = f.Empresa.CriarSetor("RH").CriarCategoria("Pessoal");
        Assert.Throws<RegraNegocioException>(() => f.Setor.MoverCategoria(filha, outro));
        Assert.Throws<RegraNegocioException>(() => f.Setor.CriarCategoria("Inválida", outro));
    }

    public void VisibilidadeRestritaDaCategoriaPaiValeParaSubcategorias()
    {
        var f = new Cenario();
        f.Categoria.DefinirVisibilidadePadrao(Visibilidade.AcessoRestrito);
        var filha = f.Setor.CriarCategoria("Denúncias", f.Categoria);
        Assert.Equal(Visibilidade.AcessoRestrito, filha.ObterVisibilidadePadraoEfetiva());
        var dados = new DadosAbertura(f.Empresa.Id, f.Usuario.Id, new(f.Setor.Id, f.Setor.Nome),
            f.Setor.FilaGeral, filha, "Título", "Descrição", Prioridade.Media, Visibilidade.CompartilhadoComSetor);
        Assert.Throws<RegraNegocioException>(() => Chamado.Abrir(dados, f.Contexto(Inicio), f.Config(f.Setor)));
        f.Setor.FilaGeral.AdicionarMembro(new VinculoSetor(f.Usuario, f.Setor.Id, PapelSetor.Atendente));
        f.Setor.FilaGeral.AutorizarAcessoRestrito(f.Usuario.Id);
        Assert.Equal(Visibilidade.AcessoRestrito, Chamado.Abrir(dados, f.Contexto(Inicio), f.Config(f.Setor)).Visibilidade);
    }

    public void MudarParaAssuntoRestritoRestringeOChamado()
    {
        var f = new Cenario();
        var restrita = f.Setor.CriarCategoria("Denúncias");
        restrita.DefinirVisibilidadePadrao(Visibilidade.AcessoRestrito);
        var subcategoria = f.Setor.CriarCategoria("Assédio", restrita);
        var versao = f.Chamado.Versao;
        Assert.Throws<RegraNegocioException>(() => f.Chamado.AlterarCategoria(subcategoria, f.Setor.FilaGeral, "Reclassificação", f.Contexto(Inicio), Acesso));
        Assert.Equal(versao, f.Chamado.Versao);
        Assert.Equal(Visibilidade.CompartilhadoComSetor, f.Chamado.Visibilidade);

        var atendente = new Usuario(f.Empresa.Id, "Ana");
        f.Setor.FilaGeral.AdicionarMembro(new VinculoSetor(atendente, f.Setor.Id, PapelSetor.Atendente));
        f.Chamado.Assumir(atendente, f.Setor.FilaGeral, new ContextoOperacao(atendente.Id, Inicio));
        var gestora = new Usuario(f.Empresa.Id, "Bia");
        f.Setor.FilaGeral.AdicionarMembro(new VinculoSetor(gestora, f.Setor.Id, PapelSetor.Atendente));
        f.Setor.FilaGeral.AutorizarAcessoRestrito(gestora.Id);
        Assert.Throws<RegraNegocioException>(() => f.Chamado.AlterarCategoria(subcategoria, f.Setor.FilaGeral, "Reclassificação", f.Contexto(Inicio), Acesso));

        f.Setor.FilaGeral.AutorizarAcessoRestrito(atendente.Id);
        f.Chamado.AlterarCategoria(subcategoria, f.Setor.FilaGeral, "Reclassificação", f.Contexto(Inicio), Acesso);
        Assert.Equal(Visibilidade.AcessoRestrito, f.Chamado.Visibilidade);
        Assert.Equal(TipoEventoChamado.VisibilidadeAlterada, f.Chamado.Eventos.Last().Tipo);
    }

    public void TransferirParaAssuntoRestritoRestringeERemoveResponsavelSemAcesso()
    {
        var f = new Cenario();
        var especialistas = f.Setor.CriarFila("Especialistas");
        var restrita = f.Setor.CriarCategoria("Auditoria");
        restrita.DefinirVisibilidadePadrao(Visibilidade.AcessoRestrito);
        var atendente = new Usuario(f.Empresa.Id, "Ana");
        var vinculo = new VinculoSetor(atendente, f.Setor.Id, PapelSetor.Atendente);
        f.Setor.FilaGeral.AdicionarMembro(vinculo);
        especialistas.AdicionarMembro(vinculo);
        f.Chamado.Assumir(atendente, f.Setor.FilaGeral, new ContextoOperacao(atendente.Id, Inicio));
        Assert.Throws<RegraNegocioException>(() => f.Chamado.Transferir(especialistas, restrita, "Auditoria",
            f.Config(f.Setor), f.Contexto(Inicio), Acesso));

        var auditora = new Usuario(f.Empresa.Id, "Clara");
        especialistas.AdicionarMembro(new VinculoSetor(auditora, f.Setor.Id, PapelSetor.Atendente));
        especialistas.AutorizarAcessoRestrito(auditora.Id);
        f.Chamado.Transferir(especialistas, restrita, "Auditoria", f.Config(f.Setor), f.Contexto(Inicio), Acesso);
        Assert.Equal(Visibilidade.AcessoRestrito, f.Chamado.Visibilidade);
        Assert.Equal<Guid?>(null, f.Chamado.ResponsavelId);
        Assert.Equal(StatusChamado.AguardandoTriagem, f.Chamado.Status);
    }

    public void HierarquiaLimitaProfundidadeInclusiveAoMover()
    {
        var f = new Cenario();
        var filha = f.Setor.CriarCategoria("Equipamentos", f.Categoria);
        var neta = f.Setor.CriarCategoria("Impressora", filha);
        Assert.Equal(3, neta.Nivel);
        Assert.Throws<RegraNegocioException>(() => f.Setor.CriarCategoria("Toner", neta));
        var raiz = f.Setor.CriarCategoria("Outra raiz");
        var outraFilha = f.Setor.CriarCategoria("Galho", raiz);
        Assert.Throws<RegraNegocioException>(() => f.Setor.MoverCategoria(filha, outraFilha));
        Assert.Equal<Guid?>(f.Categoria.Id, filha.CategoriaPaiId);
        f.Setor.MoverCategoria(neta, raiz);
        Assert.Equal(2, neta.Nivel);
    }

    public void ReativacaoRespeitaCategoriaPaiENomesDoNivel()
    {
        var f = new Cenario();
        var filha = f.Setor.CriarCategoria("ERP", f.Categoria);
        f.Categoria.Desativar();
        Assert.False(filha.AtivaNaHierarquia);
        Assert.False(filha.PodeReceberChamado);
        filha.Desativar();
        Assert.Throws<RegraNegocioException>(() => f.Setor.ReativarCategoria(filha));
        f.Setor.ReativarCategoria(f.Categoria);
        f.Setor.CriarCategoria("ERP", f.Categoria);
        Assert.Throws<RegraNegocioException>(() => f.Setor.ReativarCategoria(filha));
        Assert.False(filha.Ativa);
    }

    public void HerancaDeFilaPermiteSobrescreverInclusiveComFilaGeral()
    {
        var f = new Cenario();
        var fila = f.Setor.CriarFila("Especialistas");
        f.Categoria.ConfigurarDestino(fila);
        var filha = f.Setor.CriarCategoria("ERP", f.Categoria);
        var neta = f.Setor.CriarCategoria("Acesso", filha);
        Assert.Equal(fila.Id, f.Setor.DeterminarFilaInicial(neta));
        filha.ConfigurarDestino(f.Setor.FilaGeral);
        Assert.Equal(f.Setor.FilaGeralId, f.Setor.DeterminarFilaInicial(neta));
        filha.ConfigurarDestino(null);
        Assert.Equal(fila.Id, f.Setor.DeterminarFilaInicial(neta));
        fila.Desativar();
        Assert.Equal(f.Setor.FilaGeralId, f.Setor.DeterminarFilaInicial(neta));
    }

    public void PreQualificacaoHerdaListaMaisProximaSemRestringirAcesso()
    {
        var f = new Cenario();
        f.Categoria.PreQualificar(f.Usuario, Inicio);
        var filha = f.Setor.CriarCategoria("ERP", f.Categoria);
        Assert.True(filha.PreferencialPara(f.Usuario.Id));
        var outro = new Usuario(f.Empresa.Id, "Luiz");
        filha.PreQualificar(outro, Inicio);
        Assert.False(filha.PreferencialPara(f.Usuario.Id));
        Assert.True(filha.PreferencialPara(outro.Id));
        filha.RemoverPreQualificacao(outro.Id);
        Assert.True(filha.PreferencialPara(f.Usuario.Id));
        f.Categoria.RemoverPreQualificacao(f.Usuario.Id);
        Assert.False(filha.PossuiPreQualificados());
        Assert.False(f.Setor.FilaGeral.PodeAtender(outro.Id));
    }

    public void SetorControlaSelecaoECaminhoHistoricoSobreviveAReorganizacao()
    {
        var f = new Cenario();
        var filha = f.Setor.CriarCategoria("ERP", f.Categoria);
        f.Setor.ConfigurarRecebimentoCategoria(f.Categoria, false);
        Assert.Throws<RegraNegocioException>(() => f.Setor.DeterminarFilaInicial(f.Categoria));
        Assert.Throws<RegraNegocioException>(() => f.Chamado.AlterarCategoria(f.Categoria, f.Setor.FilaGeral, "Teste", f.Contexto(Inicio), Acesso));
        f.Chamado.AlterarCategoria(filha, f.Setor.FilaGeral, "Detalhamento", f.Contexto(Inicio), Acesso);
        f.Categoria.Renomear("Sistemas");
        f.Setor.MoverCategoria(filha, null);
        Assert.Equal("Licenças → ERP", f.Chamado.CategoriaAtual.Caminho);
        Assert.Equal("Licenças → ERP", f.Chamado.Eventos.Last().Depois["CategoriaCaminho"]);
        f.Setor.MoverCategoria(filha, f.Categoria);
        f.Categoria.Desativar();
        Assert.False(filha.PodeReceberChamado);
        Assert.Throws<RegraNegocioException>(() => f.Setor.DeterminarFilaInicial(filha));
    }

    public void AlterarExpedienteEFeriadoNaoReescreveSlaNemEtapaConcluidos()
    {
        var f = new Cenario();
        f.Chamado.ResponderEquipe("Recebido", f.Contexto(Inicio.AddHours(4)), Acesso);
        var sla = f.Chamado.CicloAtual.CiclosSla.Single(s => s.Tipo == TipoSla.PrimeiraResposta);
        f.Chamado.Aceitar(f.Contexto(Inicio.AddHours(4)), Acesso);
        var periodo = f.Chamado.CicloAtual.Periodos[0];
        f.Calendario.DefinirExpediente(DayOfWeek.Monday, [new(new(8, 0), new(10, 0))]);
        f.Calendario.RegistrarExcecao(new(new(2026, 10, 5), "Feriado", []));
        Assert.Equal(TimeSpan.FromHours(4), sla.CalcularConsumido(Inicio.AddDays(3)));
        Assert.Equal(TimeSpan.FromHours(4), periodo.TempoUtil(Inicio.AddDays(3)));
        Assert.Throws<RegraNegocioException>(() => sla.CalendarioAplicado.DefinirExpediente(DayOfWeek.Monday, []));
    }

    public void RevogacaoRemoveAtendimentoEAcessoRestrito(string tipo)
    {
        var f = new Cenario();
        var vinculo = new VinculoSetor(f.Usuario, f.Setor.Id, PapelSetor.Atendente);
        vinculo.ConcederPapel(PapelSetor.Solicitante);
        f.Setor.FilaGeral.AdicionarMembro(vinculo);
        f.Setor.FilaGeral.AutorizarAcessoRestrito(f.Usuario.Id);
        Assert.True(f.Setor.FilaGeral.PodeAcessarRestrito(f.Usuario.Id));
        if (tipo == "vinculo") vinculo.Desativar();
        if (tipo == "papel") vinculo.RevogarPapel(PapelSetor.Atendente);
        if (tipo == "usuario") f.Usuario.Desativar();
        Assert.False(f.Setor.FilaGeral.PodeAtender(f.Usuario.Id));
        Assert.False(f.Setor.FilaGeral.PodeAcessarRestrito(f.Usuario.Id));
        Assert.False(f.Setor.FilaGeral.TemAtendenteAutorizadoParaRestritos());
        Assert.ThrowsAny<Exception>(() => f.Chamado.Assumir(f.Usuario, f.Setor.FilaGeral, f.Contexto(Inicio)));
    }

    public void DuasTransferenciasPreservamPrimeiraRespostaDesdeAbertura()
    {
        var f = new Cenario();
        for (var i = 1; i <= 2; i++)
        {
            var destino = f.Empresa.CriarSetor("Destino " + i);
            f.ConfigurarSla(destino);
            var categoria = destino.CriarCategoria("Geral");
            f.Chamado.Transferir(destino.FilaGeral, categoria, "Encaminhamento",
                f.Config(destino), f.Contexto(Inicio.AddHours(i)), Acesso);
            if (i == 2)
            {
                destino.Sla.DefinirMeta(Prioridade.Urgente, TipoSla.PrimeiraResposta, new(1));
                destino.Sla.DefinirMeta(Prioridade.Urgente, TipoSla.Resolucao, new(2));
                f.Chamado.AlterarPrioridade(Prioridade.Urgente, "Urgência", destino.Sla,
                    f.Contexto(Inicio.AddHours(i)), Acesso);
            }
        }
        f.Chamado.ResponderEquipe("Primeiro retorno", f.Contexto(Inicio.AddHours(4)), Acesso);
        var sla = Assert.Single(f.Chamado.CicloAtual.CiclosSla.Where(s => s.Tipo == TipoSla.PrimeiraResposta));
        Assert.Equal(MotivoFinalizacaoSla.Respondido, sla.MotivoFinalizacao);
        Assert.Equal(TimeSpan.FromHours(4), sla.CalcularConsumido(Inicio.AddHours(4)));
        Assert.Equal(8m, sla.MetaAplicada.Valor);
        Assert.Equal(2, f.Chamado.CicloAtual.CiclosSla.Count(s => s.Tipo == TipoSla.Resolucao && s.MotivoFinalizacao == MotivoFinalizacaoSla.Transferencia));
    }

    public void HistoricoEPapeisNaoPodemSerModificadosPelasColecoesExpostas()
    {
        var f = new Cenario();
        var vinculo = new VinculoSetor(f.Usuario, f.Setor.Id, PapelSetor.Solicitante);
        Assert.Throws<NotSupportedException>(() => ((ICollection<PapelSetor>)vinculo.Papeis).Add(PapelSetor.Atendente));
        Assert.False(vinculo.PossuiPapel(PapelSetor.Atendente));
        var dados = new Dictionary<string, string?> { ["Status"] = "Original" };
        var evento = new EventoChamado(Guid.NewGuid(), f.Chamado.Id, Inicio, f.Usuario.Id, TipoEventoChamado.Aberto, null, dados, dados);
        dados["Status"] = "Alterado";
        Assert.Equal("Original", evento.Depois["Status"]);
        Assert.Throws<NotSupportedException>(() => ((IDictionary<string, string?>)evento.Depois)["Status"] = "Alterado");
    }

    public void RenomearCategoriaPreservaNomesDeAberturaETroca()
    {
        var f = new Cenario();
        f.Categoria.Renomear("Novo nome");
        Assert.Equal("Licenças", f.Chamado.CategoriaAtual.NomeNaOcorrencia);
        var nova = f.Setor.CriarCategoria("Equipamentos");
        f.Chamado.AlterarCategoria(nova, f.Setor.FilaGeral, "Correção", f.Contexto(Inicio), Acesso);
        nova.Renomear("Outro nome");
        var evento = f.Chamado.Eventos.Last();
        Assert.Equal("Licenças", evento.Antes["CategoriaNome"]);
        Assert.Equal("Equipamentos", evento.Depois["CategoriaNome"]);
    }

    public void AvaliacaoUsaPrazoGravadoNoEncerramentoMesmoAposAlterarConfiguracao()
    {
        var f = new Cenario();
        f.Setor.ConfigurarCicloVida(new(new(32), TimeSpan.FromDays(7), 1, TimeSpan.FromDays(2)));
        f.Chamado.Resolver("Resolvido", f.Config(f.Setor), f.Contexto(Inicio.AddHours(1)), Acesso);
        f.Chamado.ConfirmarSolucao(f.Contexto(Inicio.AddHours(2)));
        f.Setor.ConfigurarCicloVida(new(new(32), TimeSpan.FromDays(7), 1, TimeSpan.FromDays(30)));
        Assert.Equal(Inicio.AddHours(2).AddDays(2), f.Chamado.CicloAtual.LimiteAvaliacao);
        Assert.Throws<RegraNegocioException>(() => f.Chamado.Avaliar(5, null, f.Contexto(Inicio.AddDays(3))));
    }

    public void ProximoVencimentoAcompanhaPausasRetomadasEResolucao()
    {
        var f = new Cenario();
        Assert.Equal<DateTimeOffset?>(Inicio.AddHours(9), f.Chamado.ProximoVencimentoEm);
        f.Chamado.SolicitarInformacao("Qual usuário?", f.Contexto(Inicio.AddHours(1)), Acesso);
        Assert.Equal<DateTimeOffset?>(null, f.Chamado.ProximoVencimentoEm);
        f.Chamado.ResponderSolicitante("joao.silva", f.Contexto(Inicio.AddHours(2)));
        Assert.Equal<DateTimeOffset?>(Inicio.AddDays(1).AddHours(1), f.Chamado.ProximoVencimentoEm);
        f.Chamado.Resolver("Senha redefinida", f.Config(f.Setor), f.Contexto(Inicio.AddHours(3)), Acesso);
        Assert.Equal<DateTimeOffset?>(null, f.Chamado.ProximoVencimentoEm);
    }

    public void NotaAbaixoDaMediaExigeJustificativa()
    {
        var f = new Cenario();
        f.Chamado.Resolver("Resolvido", f.Config(f.Setor), f.Contexto(Inicio.AddHours(1)), Acesso);
        f.Chamado.ConfirmarSolucao(f.Contexto(Inicio.AddHours(2)));
        var versao = f.Chamado.Versao;
        var erro = Assert.ThrowsReturning<ValidacaoDominioException>(() => f.Chamado.Avaliar(2, "   ", f.Contexto(Inicio.AddHours(3))));
        Assert.Equal("justificativa_obrigatoria", erro.Codigo);
        Assert.Equal("comentario", erro.Campo);
        Assert.Equal(versao, f.Chamado.Versao);
        Assert.Equal<Avaliacao?>(null, f.Chamado.CicloAtual.Avaliacao);
        f.Chamado.Avaliar(2, "Demorou para retornar", f.Contexto(Inicio.AddHours(3)));
        Assert.Equal("Demorou para retornar", f.Chamado.CicloAtual.Avaliacao!.Comentario);
        Assert.True(Avaliacao.ExigeJustificativa(1));
        Assert.False(Avaliacao.ExigeJustificativa(3));
        Assert.Equal(3, new Avaliacao(3, null, f.Usuario.Id, Inicio).Nota);
    }

    public void EsperaPausaResolucaoEMensagensAdicionaisNaoReiniciamResposta()
    {
        var f = new Cenario();
        f.Chamado.SolicitarInformacao("Detalhes?", f.Contexto(Inicio.AddHours(1)), Acesso);
        f.Chamado.ResponderSolicitante("Detalhes", f.Contexto(Inicio.AddHours(3)));
        f.Chamado.ResponderSolicitante("Mais detalhes", f.Contexto(Inicio.AddHours(4)));
        Assert.Equal(StatusChamado.Aceito, f.Chamado.Status);
        Assert.Equal(Inicio.AddHours(3), f.Chamado.CicloAtual.SlaAtivo(TipoSla.ProximaResposta)!.IniciadoEm);
        Assert.Equal(TimeSpan.FromHours(2), f.Chamado.CicloAtual.SlaAtivo(TipoSla.Resolucao)!.CalcularConsumido(Inicio.AddHours(4)));
    }

    private sealed class Cenario
    {
        public CalendarioEmpresa Calendario { get; } = new("UTC");
        public Empresa Empresa { get; }
        public Setor Setor { get; }
        public Categoria Categoria { get; }
        public Usuario Usuario { get; }
        public Chamado Chamado { get; }
        public Cenario()
        {
            foreach (var dia in new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday })
                Calendario.DefinirExpediente(dia, [new(new(8, 0), new(12, 0)), new(new(13, 0), new(17, 0))]);
            Empresa = new("Empresa", Calendario);
            Setor = Empresa.CriarSetor("TI");
            ConfigurarSla(Setor);
            Categoria = Setor.CriarCategoria("Licenças");
            Usuario = new(Empresa.Id, "Carlos");
            Chamado = Chamado.Abrir(new(Empresa.Id, Usuario.Id, new(Setor.Id, Setor.Nome),
                Setor.FilaGeral, Categoria,
                "Acesso", "Preciso de acesso", Prioridade.Media, Visibilidade.CompartilhadoComSetor), Contexto(Inicio), Config(Setor));
        }
        public void ConfigurarSla(Setor setor)
        {
            foreach (var prioridade in Enum.GetValues<Prioridade>())
                foreach (var tipo in Enum.GetValues<TipoSla>()) setor.Sla.DefinirMeta(prioridade, tipo, new(8));
        }
        public ContextoOperacao Contexto(DateTimeOffset instante) => new(Usuario.Id, instante);
        public ConfiguracaoAplicada Config(Setor setor) => new(new(setor.Id, setor.Nome), setor.Sla, setor.ObterCicloVida(Empresa.PadraoCicloVida), Calendario);
    }

    private sealed class Permitir : IAutorizacaoChamado
    {
        public bool PodeVisualizar(Chamado c, Guid u) => true;
        public void ExigirAtendimento(Chamado c, Guid u) { }
        public void ExigirTransferencia(Chamado c, Guid u, Fila f) { }
        public void ExigirCancelamento(Chamado c, Guid u) { }
        public void ExigirGestaoSetor(Guid u, Guid s) { }
    }
}
