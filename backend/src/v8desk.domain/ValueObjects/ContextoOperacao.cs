namespace v8desk.domain.ValueObjects;

public sealed record ContextoOperacao
{
    public Guid AutorId { get; }
    public DateTimeOffset Agora { get; }

    public ContextoOperacao(Guid autorId, DateTimeOffset agora)
    {
        AutorId = Guarda.Identificador(autorId, "o autor da ação");
        Agora = Guarda.Instante(agora, "a data da ação");
    }
}
