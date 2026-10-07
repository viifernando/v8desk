namespace v8desk.domain.ValueObjects;

public sealed record IntervaloExpediente
{
    public TimeOnly Inicio { get; }
    public TimeOnly Fim { get; }

    public IntervaloExpediente(TimeOnly inicio, TimeOnly fim)
    {
        if (fim <= inicio)
            throw new ValidacaoDominioException("expediente_invalido", "expediente", "O horário de término deve ser depois do horário de início.");

        Inicio = inicio;
        Fim = fim;
    }

    public TimeSpan Duracao => Fim - Inicio;

    internal static IReadOnlyList<IntervaloExpediente> Normalizar(IEnumerable<IntervaloExpediente> intervalos)
    {
        ArgumentNullException.ThrowIfNull(intervalos);

        var lista = intervalos.ToList();

        if (lista.Any(intervalo => intervalo is null))
            throw new ValidacaoDominioException("expediente_invalido", "expediente", "Preencha todos os intervalos de expediente.");

        var ordenados = lista.OrderBy(intervalo => intervalo.Inicio).ToList();

        for (var i = 1; i < ordenados.Count; i++)
        {
            if (ordenados[i].Inicio < ordenados[i - 1].Fim)
                throw new ValidacaoDominioException("expediente_sobreposto", "expediente", "Os intervalos de expediente não podem ter horários sobrepostos.");
        }

        return ordenados.AsReadOnly();
    }
}
