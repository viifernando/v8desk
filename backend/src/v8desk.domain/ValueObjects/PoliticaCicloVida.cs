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
            throw new ValidacaoDominioException("prazo_invalido", "prazoReabertura", "O prazo para reabrir deve ser maior que zero.");
        if (diasUteisAntecedenciaLembrete < 0)
            throw new ValidacaoDominioException("antecedencia_invalida", "diasUteisAntecedenciaLembrete", "A antecedência do lembrete não pode ser negativa. Use zero para desativá-lo.");

        if (prazoAvaliacao <= TimeSpan.Zero)
            throw new ValidacaoDominioException("prazo_invalido", "prazoAvaliacao", "O prazo para avaliar deve ser maior que zero.");
        PrazoAvaliacao = prazoAvaliacao;
        PrazoValidacao = prazoValidacao;
        PrazoReabertura = prazoReabertura;
        DiasUteisAntecedenciaLembrete = diasUteisAntecedenciaLembrete;
    }

    public static PoliticaCicloVida Padrao =>
        new(new HorasUteis(32), TimeSpan.FromDays(7), 1, TimeSpan.FromDays(7));
}
