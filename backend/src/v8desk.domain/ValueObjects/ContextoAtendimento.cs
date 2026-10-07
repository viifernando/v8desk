namespace v8desk.domain.ValueObjects;

public sealed record ContextoAtendimento(
    ReferenciaHistorica Setor,
    ReferenciaHistorica Fila,
    ReferenciaHistorica? Responsavel);
