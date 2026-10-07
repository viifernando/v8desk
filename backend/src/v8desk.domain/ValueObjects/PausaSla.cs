namespace v8desk.domain.ValueObjects;

public sealed record PausaSla(DateTimeOffset Inicio, DateTimeOffset? Fim, string Motivo);
