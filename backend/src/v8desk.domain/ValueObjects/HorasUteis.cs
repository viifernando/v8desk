namespace v8desk.domain.ValueObjects;

public sealed record HorasUteis
{
    public decimal Valor { get; }

    public HorasUteis(decimal valor)
    {
        if (valor <= 0)
            throw new ArgumentOutOfRangeException(nameof(valor));

        Valor = valor;
    }

    public TimeSpan ParaTimeSpan() => TimeSpan.FromHours((double)Valor);
}
