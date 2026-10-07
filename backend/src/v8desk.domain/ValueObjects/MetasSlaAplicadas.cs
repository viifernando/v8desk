namespace v8desk.domain.ValueObjects;

public sealed record MetasSlaAplicadas(
    Guid SetorId,
    Prioridade Prioridade,
    long VersaoPolitica,
    CalendarioEmpresa Calendario,
    HorasUteis PrimeiraResposta,
    HorasUteis ProximaResposta,
    HorasUteis Resolucao)
{
    public long VersaoCalendario => Calendario.Versao;
    public static MetasSlaAplicadas De(PoliticaSla politica, Guid setorId,
        Prioridade prioridade, CalendarioEmpresa calendario)
    {
        ArgumentNullException.ThrowIfNull(politica);

        return new MetasSlaAplicadas(
            setorId,
            prioridade,
            politica.Versao,
            calendario.CriarSnapshot(),
            politica.ObterMeta(prioridade, TipoSla.PrimeiraResposta).Prazo,
            politica.ObterMeta(prioridade, TipoSla.ProximaResposta).Prazo,
            politica.ObterMeta(prioridade, TipoSla.Resolucao).Prazo);
    }

    public HorasUteis Obter(TipoSla tipo) => tipo switch
    {
        TipoSla.PrimeiraResposta => PrimeiraResposta,
        TipoSla.ProximaResposta => ProximaResposta,
        TipoSla.Resolucao => Resolucao,
        _ => throw new ArgumentOutOfRangeException(nameof(tipo))
    };
}
