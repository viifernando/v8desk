namespace v8desk.domain.ValueObjects;

public sealed record RevisaoMetaSla(HorasUteis Anterior, HorasUteis Nova, DateTimeOffset AlteradaEm);
