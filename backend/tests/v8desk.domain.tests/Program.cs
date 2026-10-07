using v8desk.domain.tests;

var suite = new RegressoesDominioTests();
var casos = new (string Nome, Action Executar)[]
{
    ("Hierarquia sem ciclos e entre setores", suite.HierarquiaRejeitaCiclosEOutrosSetoresSemModificarArvore),
    ("Herança de fila", suite.HerancaDeFilaPermiteSobrescreverInclusiveComFilaGeral),
    ("Herança de pré-qualificação", suite.PreQualificacaoHerdaListaMaisProximaSemRestringirAcesso),
    ("Seleção e caminho histórico", suite.SetorControlaSelecaoECaminhoHistoricoSobreviveAReorganizacao),
    ("Calendário histórico", suite.AlterarExpedienteEFeriadoNaoReescreveSlaNemEtapaConcluidos),
    ("Revogar vínculo", () => suite.RevogacaoRemoveAtendimentoEAcessoRestrito("vinculo")),
    ("Revogar papel", () => suite.RevogacaoRemoveAtendimentoEAcessoRestrito("papel")),
    ("Desativar usuário", () => suite.RevogacaoRemoveAtendimentoEAcessoRestrito("usuario")),
    ("Primeira resposta após transferências", suite.DuasTransferenciasPreservamPrimeiraRespostaDesdeAbertura),
    ("Coleções protegidas", suite.HistoricoEPapeisNaoPodemSerModificadosPelasColecoesExpostas),
    ("Nomes históricos", suite.RenomearCategoriaPreservaNomesDeAberturaETroca),
    ("Prazo da avaliação", suite.AvaliacaoUsaPrazoGravadoNoEncerramentoMesmoAposAlterarConfiguracao),
    ("Pausas e mensagens consecutivas", suite.EsperaPausaResolucaoEMensagensAdicionaisNaoReiniciamResposta)
};
var falhas = 0;
foreach (var caso in casos)
{
    try { caso.Executar(); Console.WriteLine($"PASSOU: {caso.Nome}"); }
    catch (Exception ex) { falhas++; Console.Error.WriteLine($"FALHOU: {caso.Nome}\n{ex}"); }
}
Console.WriteLine($"{casos.Length - falhas}/{casos.Length} passaram.");
return falhas == 0 ? 0 : 1;

namespace v8desk.domain.tests
{
    internal static class Assert
    {
        public static void True(bool valor) { if (!valor) throw new Exception("Esperado true."); }
        public static void False(bool valor) => True(!valor);
        public static void Equal<T>(T esperado, T atual)
        {
            if (!EqualityComparer<T>.Default.Equals(esperado, atual))
                throw new Exception($"Esperado {esperado}, recebido {atual}.");
        }
        public static T Single<T>(IEnumerable<T> itens) => itens.Single();
        public static void Throws<T>(Action executar) where T : Exception
        {
            try { executar(); }
            catch (T) { return; }
            throw new Exception($"Esperada exceção {typeof(T).Name}.");
        }
        public static void ThrowsAny<T>(Action executar) where T : Exception => Throws<T>(executar);
    }
}
