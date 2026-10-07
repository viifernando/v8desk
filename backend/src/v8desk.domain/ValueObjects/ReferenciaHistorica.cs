namespace v8desk.domain.ValueObjects;

public sealed record ReferenciaHistorica
{
    public Guid Id { get; }
    public string NomeNaOcorrencia { get; }

    public ReferenciaHistorica(Guid id, string nomeNaOcorrencia)
    {
        Id = Guarda.Identificador(id, "o identificador da referência");
        NomeNaOcorrencia = Guarda.Texto(nomeNaOcorrencia, "o nome da referência");
    }
}
