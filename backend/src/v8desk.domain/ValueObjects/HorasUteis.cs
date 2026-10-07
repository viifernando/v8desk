namespace v8desk.domain.ValueObjects;

public sealed record HorasUteis
{
    public decimal Valor { get; }

    public HorasUteis(decimal valor)
    {
        if (valor < 1m / TimeSpan.TicksPerHour || valor > (decimal)TimeSpan.MaxValue.Ticks / TimeSpan.TicksPerHour)
            throw new ValidacaoDominioException("prazo_invalido", "horasUteis",
                "Informe um prazo em horas úteis maior que zero e dentro do intervalo permitido.");

        Valor = valor;
    }

    public TimeSpan ParaTimeSpan() => TimeSpan.FromTicks((long)(Valor * TimeSpan.TicksPerHour));
}
