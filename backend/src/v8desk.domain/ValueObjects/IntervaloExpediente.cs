namespace v8desk.domain.ValueObjects;

public sealed record IntervaloExpediente
{
    public TimeOnly Inicio { get; }
    public TimeOnly Fim { get; }

    public IntervaloExpediente(TimeOnly inicio, TimeOnly fim)
    {
        if (fim <= inicio)
            throw new RegraNegocioException("O fim do intervalo de expediente deve ser posterior ao início.");

        Inicio = inicio;
        Fim = fim;
    }

    public TimeSpan Duracao => Fim - Inicio;

    internal static IReadOnlyList<IntervaloExpediente> Normalizar(IEnumerable<IntervaloExpediente> intervalos)
    {
        ArgumentNullException.ThrowIfNull(intervalos);

        var lista = intervalos.ToList();

        if (lista.Any(intervalo => intervalo is null))
            throw new ArgumentException("Intervalos de expediente não podem ser nulos.", nameof(intervalos));

        var ordenados = lista.OrderBy(intervalo => intervalo.Inicio).ToList();

        for (var i = 1; i < ordenados.Count; i++)
        {
            if (ordenados[i].Inicio < ordenados[i - 1].Fim)
                throw new RegraNegocioException("Intervalos de expediente não podem se sobrepor.");
        }

        return ordenados.AsReadOnly();
    }
}
