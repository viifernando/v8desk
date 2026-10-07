using v8desk.domain.tests;

var suite = new RegressoesDominioTests();
var casos = new (string Nome, Action Executar)[]
{
    ("Snapshots JSONB", PersistenciaTests.SnapshotsJsonPreservamCalendarioAnexosECaminho),
    ("Conflitos do banco com mensagens amigáveis", PersistenciaTests.ErrosDoBancoNaoExponhemDetalhes),
    ("Grafo e outbox transacional preparada", PersistenciaTests.GrafoDeChamadoRecebeEmpresaEOutboxSemDependenciaDeBanco),
    ("Modelo EF/PostgreSQL", PersistenciaTests.ModeloPostgresGeraSchemaComIndicesConcorrenciaEIsolamento),
    ("Filtro de empresa no SQL", PersistenciaTests.FiltroDeEmpresaEstaNoSqlETrocaPorContexto),
    ("Gravação entre empresas bloqueada", PersistenciaTests.GravacaoDeOutraEmpresaFalhaAntesDeAcessarBanco),
    ("Entradas inválidas e mensagens", suite.EntradasInvalidasTemMensagensClarasECodigos),
    ("Cronologia sem mutação parcial", suite.AcaoRetroativaNaoDeixaMutacaoParcial),
    ("Abertura e limites", suite.AberturaRejeitaTextosExcessivosEFilaDeOutroSetor),
    ("Visibilidade restrita na abertura", suite.CategoriaRestritaNaoPodeSerAbertaCompartilhada),
    ("Isolamento entre empresas", suite.UsuariosDeOutraEmpresaNaoPodemIntegrarFilaOuPreQualificacao),
    ("Erros HTTP e proteção dos detalhes", ApiErrorsTests.ErrosConhecidosSaoAmigaveisEFalhasInternasNaoVazamDetalhes),
    ("Hierarquia sem ciclos e entre setores", suite.HierarquiaRejeitaCiclosEOutrosSetoresSemModificarArvore),
    ("Herança de fila", suite.HerancaDeFilaPermiteSobrescreverInclusiveComFilaGeral),
    ("Visibilidade restrita herdada", suite.VisibilidadeRestritaDaCategoriaPaiValeParaSubcategorias),
    ("Mudar para assunto restrito", suite.MudarParaAssuntoRestritoRestringeOChamado),
    ("Transferir para assunto restrito", suite.TransferirParaAssuntoRestritoRestringeERemoveResponsavelSemAcesso),
    ("Profundidade máxima da hierarquia", suite.HierarquiaLimitaProfundidadeInclusiveAoMover),
    ("Reativação na hierarquia", suite.ReativacaoRespeitaCategoriaPaiENomesDoNivel),
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
    ("Justificativa em nota baixa", suite.NotaAbaixoDaMediaExigeJustificativa),
    ("Próximo vencimento do chamado", suite.ProximoVencimentoAcompanhaPausasRetomadasEResolucao),
    ("Paginação: SQL de todas as ordens", PaginacaoTests.TodasAsOrdensGeramSqlComCursorEFiltros),
    ("Paginação: cursor inválido", PaginacaoTests.CursorAdulteradoOuDeOutraOrdemEhRecusado),
    ("Pausas e mensagens consecutivas", suite.EsperaPausaResolucaoEMensagensAdicionaisNaoReiniciamResposta)
};
var falhas = 0;
foreach (var caso in casos)
{
    try { caso.Executar(); Console.WriteLine($"PASSOU: {caso.Nome}"); }
    catch (Exception ex) { falhas++; Console.Error.WriteLine($"FALHOU: {caso.Nome}\n{ex}"); }
}
Console.WriteLine($"{casos.Length - falhas}/{casos.Length} passaram.");
var conexaoIntegracao = Environment.GetEnvironmentVariable("V8DESK_POSTGRES_TEST_CONNECTION");
if (!string.IsNullOrWhiteSpace(conexaoIntegracao))
{
    try { await PostgresIntegracaoTests.ExecutarAsync(conexaoIntegracao); }
    catch (Exception erro) { falhas++; Console.Error.WriteLine($"FALHOU: integração PostgreSQL\n{erro}"); }
}
else Console.WriteLine("PostgreSQL real: não executado (configure V8DESK_POSTGRES_TEST_CONNECTION para habilitar).");
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
        public static T ThrowsReturning<T>(Action executar) where T : Exception
        {
            try { executar(); }
            catch (T erro) { return erro; }
            throw new Exception($"Esperada exceção {typeof(T).Name}.");
        }
    }
}
