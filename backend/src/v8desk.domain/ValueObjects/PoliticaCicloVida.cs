namespace v8desk.domain.ValueObjects;

public sealed record PoliticaCicloVida
{
    public HorasUteis PrazoValidacao { get; }
    public TimeSpan PrazoReabertura { get; }
    public int DiasUteisAntecedenciaLembrete { get; }

    public PoliticaCicloVida(HorasUteis prazoValidacao, TimeSpan prazoReabertura,
        int diasUteisAntecedenciaLembrete)
    {
        ArgumentNullException.ThrowIfNull(prazoValidacao);
        if (prazoReabertura <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(prazoReabertura));
        if (diasUteisAntecedenciaLembrete < 0)
            throw new ArgumentOutOfRangeException(nameof(diasUteisAntecedenciaLembrete));

        PrazoValidacao = prazoValidacao;
        PrazoReabertura = prazoReabertura;
        DiasUteisAntecedenciaLembrete = diasUteisAntecedenciaLembrete;
    }

    public static PoliticaCicloVida Padrao =>
        new(new HorasUteis(32), TimeSpan.FromDays(7), 1);
}
