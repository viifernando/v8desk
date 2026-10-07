namespace v8desk.domain.ValueObjects;

public sealed record ConfiguracaoAplicada
{
    public ReferenciaHistorica Setor { get; }
    public PoliticaSla PoliticaSla { get; }
    public PoliticaCicloVida CicloVida { get; }
    public CalendarioEmpresa Calendario { get; }

    public ConfiguracaoAplicada(ReferenciaHistorica setor, PoliticaSla politicaSla,
        PoliticaCicloVida cicloVida, CalendarioEmpresa calendario)
    {
        ArgumentNullException.ThrowIfNull(setor);
        ArgumentNullException.ThrowIfNull(politicaSla);
        ArgumentNullException.ThrowIfNull(cicloVida);
        ArgumentNullException.ThrowIfNull(calendario);

        Setor = setor;
        PoliticaSla = politicaSla;
        CicloVida = cicloVida;
        Calendario = calendario;
    }
}
