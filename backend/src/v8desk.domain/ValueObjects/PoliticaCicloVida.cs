namespace v8desk.domain.ValueObjects;

public sealed record PoliticaCicloVida
{
    public HorasUteis PrazoValidacao { get; }
    public TimeSpan PrazoReabertura { get; }
    public TimeSpan PrazoAvaliacao { get; }
    public int DiasUteisAntecedenciaLembrete { get; }

    public PoliticaCicloVida(HorasUteis prazoValidacao, TimeSpan prazoReabertura,
        int diasUteisAntecedenciaLembrete, TimeSpan prazoAvaliacao)
    {
        ArgumentNullException.ThrowIfNull(prazoValidacao);
        if (prazoReabertura <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(prazoReabertura));
        if (diasUteisAntecedenciaLembrete < 0)
            throw new ArgumentOutOfRangeException(nameof(diasUteisAntecedenciaLembrete));

        if (prazoAvaliacao <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(prazoAvaliacao));
        PrazoAvaliacao = prazoAvaliacao;
        PrazoValidacao = prazoValidacao;
        PrazoReabertura = prazoReabertura;
        DiasUteisAntecedenciaLembrete = diasUteisAntecedenciaLembrete;
    }

    public static PoliticaCicloVida Padrao =>
        new(new HorasUteis(32), TimeSpan.FromDays(7), 1, TimeSpan.FromDays(7));
}
