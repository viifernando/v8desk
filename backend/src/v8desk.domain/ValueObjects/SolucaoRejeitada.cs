namespace v8desk.domain.ValueObjects;

public sealed record SolucaoRejeitada(Solucao Solucao, string Motivo, DateTimeOffset RejeitadaEm);
