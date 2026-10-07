namespace v8desk.domain.Entities;

public sealed class PeriodoEtapa
{
    public Guid Id { get; }
    public StatusChamado Status { get; }
    public ContextoAtendimento Contexto { get; }
    public DateTimeOffset Entrada { get; }
    public DateTimeOffset? Saida { get; private set; }
    public bool Aberto => Saida is null;

    internal PeriodoEtapa(StatusChamado status, ContextoAtendimento contexto, DateTimeOffset entrada)
    {
        ArgumentNullException.ThrowIfNull(contexto);

        Id = Guid.CreateVersion7();
        Status = status;
        Contexto = contexto;
        Entrada = entrada;
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

    public TimeSpan TempoUtil(DateTimeOffset agora, CalendarioEmpresa calendario)
    {
        ArgumentNullException.ThrowIfNull(calendario);
        return calendario.CalcularTempoUtil(Entrada, Saida ?? agora);
    }
}
