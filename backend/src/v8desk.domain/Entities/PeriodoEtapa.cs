namespace v8desk.domain.Entities;

public sealed class PeriodoEtapa
{
#pragma warning disable CS8618 // Materialização: valores preenchidos pelo EF.
    private PeriodoEtapa() { }
#pragma warning restore CS8618

    public Guid Id { get; private set; }
    public StatusChamado Status { get; private set; }
    public CalendarioEmpresa CalendarioAplicado { get; private set; }
    public ContextoAtendimento Contexto { get; private set; }
    public DateTimeOffset Entrada { get; private set; }
    public DateTimeOffset? Saida { get; private set; }
    public bool Aberto => Saida is null;

    internal PeriodoEtapa(StatusChamado status, ContextoAtendimento contexto, DateTimeOffset entrada, CalendarioEmpresa calendario)
    {
        ArgumentNullException.ThrowIfNull(contexto);

        Id = Guid.CreateVersion7();
        Status = status;
        Contexto = contexto;
        Entrada = entrada;
        CalendarioAplicado = calendario.CriarSnapshot();
    }

    internal void Finalizar(DateTimeOffset agora)
    {
        if (!Aberto)
            throw new RegraNegocioException("O período já foi finalizado.");
        if (agora < Entrada)
            throw new RegraNegocioException("A saída não pode ser anterior à entrada.");

        Saida = agora;
    }

    public TimeSpan TempoCorrido(DateTimeOffset agora)
    {
        var fim = Saida ?? agora;
        return fim > Entrada ? fim - Entrada : TimeSpan.Zero;
    }

    public TimeSpan TempoUtil(DateTimeOffset agora)
    {
        return CalendarioAplicado.CalcularTempoUtil(Entrada, Saida ?? agora);
    }
}
