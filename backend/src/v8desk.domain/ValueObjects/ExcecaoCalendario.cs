namespace v8desk.domain.ValueObjects;

public sealed record ExcecaoCalendario
{
    public DateOnly Data { get; }
    public string Motivo { get; }
    public IReadOnlyList<IntervaloExpediente> Intervalos { get; }

    public ExcecaoCalendario(DateOnly data, string motivo, IReadOnlyCollection<IntervaloExpediente> intervalos)
    {
        Data = data;
        Motivo = Guarda.Texto(motivo, "o motivo");
        Intervalos = IntervaloExpediente.Normalizar(intervalos);
    }
}
