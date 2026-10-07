namespace v8desk.domain.ValueObjects;

public sealed record CorrecaoMensagem(
    string Texto, string Motivo, Guid AutorId, DateTimeOffset CorrigidaEm);
