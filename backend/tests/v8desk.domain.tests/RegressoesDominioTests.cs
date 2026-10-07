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
        Assert.Throws<RegraNegocioException>(() => f.Chamado.AlterarCategoria(f.Categoria, "Teste", f.Contexto(Inicio), Acesso));
        f.Chamado.AlterarCategoria(filha, "Detalhamento", f.Contexto(Inicio), Acesso);
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
        f.Chamado.AlterarCategoria(nova, "Correção", f.Contexto(Inicio), Acesso);
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
                new(Setor.FilaGeral.Id, Setor.FilaGeral.Nome), Categoria,
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
