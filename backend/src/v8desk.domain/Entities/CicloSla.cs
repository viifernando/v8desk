namespace v8desk.domain.Entities;

public sealed class CicloSla
{
    private readonly List<PausaSla> _pausas = new();
    private readonly List<RevisaoMetaSla> _revisoesMeta = new();

    public Guid Id { get; } = Guid.CreateVersion7();
    public TipoSla Tipo { get; }
    public Guid SetorId { get; }
    public DateTimeOffset IniciadoEm { get; }
    public DateTimeOffset? FinalizadoEm { get; private set; }
    public MotivoFinalizacaoSla? MotivoFinalizacao { get; private set; }
    public HorasUteis MetaAplicada { get; private set; }
    public long VersaoPoliticaAplicada { get; }
    public long VersaoCalendarioAplicada => CalendarioAplicado.Versao;
    public CalendarioEmpresa CalendarioAplicado { get; }
    public IReadOnlyList<PausaSla> Pausas => _pausas.AsReadOnly();
    public IReadOnlyList<RevisaoMetaSla> RevisoesMeta => _revisoesMeta.AsReadOnly();
    public bool Ativo => FinalizadoEm is null;
    public bool Pausado => _pausas.Count > 0 && _pausas[^1].Fim is null;

    internal CicloSla(TipoSla tipo, Guid setorId, DateTimeOffset iniciadoEm, HorasUteis meta, long versaoPolitica, CalendarioEmpresa calendario)
    {
        ArgumentNullException.ThrowIfNull(meta);

        Tipo = Guarda.Definido(tipo, "o tipo de SLA");
        SetorId = Guarda.Identificador(setorId, "o setor");
        IniciadoEm = iniciadoEm;
        MetaAplicada = meta;
        VersaoPoliticaAplicada = versaoPolitica;
        CalendarioAplicado = calendario.CriarSnapshot();
    }

    internal void Pausar(DateTimeOffset agora, string motivo)
    {
        GarantirAtivo();

        if (Pausado)
            throw new RegraNegocioException("O ciclo de SLA já está pausado.");

        if (agora < IniciadoEm)
            throw new RegraNegocioException("A pausa não pode começar antes do início do ciclo de SLA.");

        if (_pausas.Count > 0 && agora < _pausas[^1].Fim)
            throw new RegraNegocioException("A pausa não pode começar antes do fim da pausa anterior.");

        _pausas.Add(new PausaSla(agora, null, Guarda.Texto(motivo, "o motivo")));
    }

    internal void Retomar(DateTimeOffset agora)
    {
        if (!Pausado)
            throw new RegraNegocioException("O ciclo de SLA não está pausado.");

        var pausa = _pausas[^1];

        if (agora < pausa.Inicio)
            throw new RegraNegocioException("A retomada não pode ocorrer antes do início da pausa.");

        _pausas[^1] = pausa with { Fim = agora };
    }

    internal void Finalizar(DateTimeOffset agora, MotivoFinalizacaoSla motivo)
    {
        GarantirAtivo();
        Guarda.Definido(motivo, "o motivo de finalização");

        if (agora < IniciadoEm)
            throw new RegraNegocioException("O ciclo de SLA não pode ser finalizado antes do seu início.");

        if (Pausado)
            Retomar(agora);

        FinalizadoEm = agora;
        MotivoFinalizacao = motivo;
    }

    internal void AlterarMeta(HorasUteis novaMeta, DateTimeOffset agora)
    {
        ArgumentNullException.ThrowIfNull(novaMeta);
        GarantirAtivo();

        if (novaMeta == MetaAplicada)
            return;

        _revisoesMeta.Add(new RevisaoMetaSla(MetaAplicada, novaMeta, agora));
        MetaAplicada = novaMeta;
    }

    public TimeSpan CalcularConsumido(DateTimeOffset agora)
    {
        var calendario = CalendarioAplicado;
        var fim = FinalizadoEm ?? agora;

        if (fim <= IniciadoEm)
            return TimeSpan.Zero;

        var consumido = calendario.CalcularTempoUtil(IniciadoEm, fim);

        foreach (var pausa in _pausas)
        {
            var fimPausa = pausa.Fim is { } fimDefinido && fimDefinido < fim ? fimDefinido : fim;
            consumido -= calendario.CalcularTempoUtil(pausa.Inicio, fimPausa);
        }

        return consumido > TimeSpan.Zero ? consumido : TimeSpan.Zero;
    }

    public decimal CalcularPercentualConsumido(DateTimeOffset agora) =>
        (decimal)CalcularConsumido(agora).TotalHours / MetaAplicada.Valor * 100;

    public bool EstaVencido(DateTimeOffset agora) =>
        CalcularConsumido(agora) > MetaAplicada.ParaTimeSpan();

    public SituacaoSla ObterSituacao(DateTimeOffset agora)
    {
        if (Ativo)
            return SituacaoSla.EmAndamento;

        if (MotivoFinalizacao is MotivoFinalizacaoSla.Transferencia or MotivoFinalizacaoSla.Cancelado)
            return SituacaoSla.Interrompido;

        return EstaVencido(agora) ? SituacaoSla.Vencido : SituacaoSla.Cumprido;
    }

    private void GarantirAtivo()
    {
        if (!Ativo)
            throw new RegraNegocioException("O ciclo de SLA já foi finalizado.");
    }
}
