namespace v8desk.domain.ValueObjects;

public sealed record MetasSlaAplicadas(
    Guid SetorId,
    Prioridade Prioridade,
    long VersaoPolitica,
    long VersaoCalendario,
    HorasUteis PrimeiraResposta,
    HorasUteis ProximaResposta,
    HorasUteis Resolucao)
{
    public static MetasSlaAplicadas De(PoliticaSla politica, Guid setorId,
        Prioridade prioridade, long versaoCalendario)
    {
        ArgumentNullException.ThrowIfNull(politica);

        return new MetasSlaAplicadas(
            setorId,
            prioridade,
            politica.Versao,
            versaoCalendario,
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
