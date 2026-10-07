namespace v8desk.domain.Entities;

public sealed class CicloAtendimento
{
    private readonly List<PeriodoEtapa> _periodos = new();
    private readonly List<CicloSla> _ciclosSla = new();
    private readonly List<SolucaoRejeitada> _solucoesRejeitadas = new();

    public Guid Id { get; }
    public int Numero { get; }
    public DateTimeOffset IniciadoEm { get; }
    public DateTimeOffset? EncerradoEm { get; private set; }
    public MotivoEncerramento? MotivoDeEncerramento { get; private set; }
    public Solucao? Solucao { get; private set; }
    public Avaliacao? Avaliacao { get; private set; }
    public IReadOnlyList<PeriodoEtapa> Periodos => _periodos.AsReadOnly();
    public IReadOnlyList<CicloSla> CiclosSla => _ciclosSla.AsReadOnly();
    public IReadOnlyList<SolucaoRejeitada> SolucoesRejeitadas => _solucoesRejeitadas.AsReadOnly();
    public bool Encerrado => EncerradoEm is not null;
    public PeriodoEtapa? PeriodoAtual => _periodos.LastOrDefault(p => p.Aberto);
    public bool AguardandoRespostaEquipe =>
        SlaAtivo(TipoSla.PrimeiraResposta) is not null || SlaAtivo(TipoSla.ProximaResposta) is not null;

    internal CicloAtendimento(int numero, DateTimeOffset iniciadoEm)
    {
        if (numero < 1) throw new ArgumentOutOfRangeException(nameof(numero));

        Id = Guid.CreateVersion7();
        Numero = numero;
        IniciadoEm = iniciadoEm;
    }

    public CicloSla? SlaAtivo(TipoSla tipo) =>
        _ciclosSla.FirstOrDefault(s => s.Tipo == tipo && s.Ativo);

    internal void IniciarPeriodo(StatusChamado status, ContextoAtendimento contexto, DateTimeOffset agora)
    {
        ExigirAberto();
        FinalizarPeriodo(agora);
        _periodos.Add(new PeriodoEtapa(status, contexto, agora));
    }

    internal void FinalizarPeriodo(DateTimeOffset agora) => PeriodoAtual?.Finalizar(agora);

    internal void IniciarSla(TipoSla tipo, MetasSlaAplicadas metas, DateTimeOffset agora)
    {
        ArgumentNullException.ThrowIfNull(metas);
        ExigirAberto();
        if (SlaAtivo(tipo) is not null)
            throw new RegraNegocioException($"Já existe SLA de {tipo} em andamento.");

        _ciclosSla.Add(new CicloSla(tipo, metas.SetorId, agora, metas.Obter(tipo),
            metas.VersaoPolitica, metas.VersaoCalendario));
    }

    internal void IniciarProximaRespostaSeNecessario(MetasSlaAplicadas metas, DateTimeOffset agora)
    {
        if (AguardandoRespostaEquipe) return;
        IniciarSla(TipoSla.ProximaResposta, metas, agora);
    }

    internal void FinalizarEsperasDeResposta(DateTimeOffset agora, MotivoFinalizacaoSla motivo)
    {
        foreach (var sla in Ativos().Where(s => s.Tipo != TipoSla.Resolucao).ToList())
            sla.Finalizar(agora, motivo);
    }

    internal void PausarResolucao(DateTimeOffset agora, string motivo) =>
        ObterResolucaoAtiva().Pausar(agora, motivo);

    internal void RetomarResolucao(DateTimeOffset agora) =>
        ObterResolucaoAtiva().Retomar(agora);

    internal void FinalizarResolucao(DateTimeOffset agora, MotivoFinalizacaoSla motivo) =>
        ObterResolucaoAtiva().Finalizar(agora, motivo);

    internal void InterromperSlas(DateTimeOffset agora, MotivoFinalizacaoSla motivo)
    {
        foreach (var sla in Ativos().ToList())
            sla.Finalizar(agora, motivo);
    }

    internal void AplicarMetas(MetasSlaAplicadas metas, DateTimeOffset agora)
    {
        ArgumentNullException.ThrowIfNull(metas);
        foreach (var sla in Ativos())
            sla.AlterarMeta(metas.Obter(sla.Tipo), agora);
    }

    internal void RegistrarSolucao(Solucao solucao)
    {
        ArgumentNullException.ThrowIfNull(solucao);
        ExigirAberto();
        if (Solucao is not null)
            throw new RegraNegocioException("O ciclo já possui solução registrada.");

        Solucao = solucao;
    }

    internal void RejeitarSolucao(string motivo, DateTimeOffset agora)
    {
        ExigirAberto();
        if (Solucao is null)
            throw new RegraNegocioException("Não há solução para rejeitar.");

        _solucoesRejeitadas.Add(new SolucaoRejeitada(Solucao, motivo, agora));
        Solucao = null;
    }

    internal void Encerrar(DateTimeOffset agora, MotivoEncerramento motivo)
    {
        ExigirAberto();
        FinalizarPeriodo(agora);
        EncerradoEm = agora;
        MotivoDeEncerramento = motivo;
    }

    internal void RegistrarAvaliacao(Avaliacao avaliacao)
    {
        ArgumentNullException.ThrowIfNull(avaliacao);
        if (MotivoDeEncerramento is not (MotivoEncerramento.ConfirmacaoSolicitante or MotivoEncerramento.PrazoExpirado))
            throw new RegraNegocioException("Somente ciclos encerrados podem ser avaliados.");
        if (Avaliacao is not null)
            throw new RegraNegocioException("O ciclo já foi avaliado.");

        Avaliacao = avaliacao;
    }

    private IEnumerable<CicloSla> Ativos() => _ciclosSla.Where(s => s.Ativo);

    private CicloSla ObterResolucaoAtiva() =>
        SlaAtivo(TipoSla.Resolucao)
        ?? throw new RegraNegocioException("Não há SLA de resolução em andamento.");

    private void ExigirAberto()
    {
        if (Encerrado)
            throw new RegraNegocioException("O ciclo de atendimento já foi encerrado.");
    }
}
