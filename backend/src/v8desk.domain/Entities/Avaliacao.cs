namespace v8desk.domain.Entities;

public sealed class Avaliacao
{
    public int Nota { get; }
    public string? Comentario { get; }
    public Guid SolicitanteId { get; }
    public DateTimeOffset CriadaEm { get; }

    public Avaliacao(int nota, string? comentario, Guid solicitanteId, DateTimeOffset criadaEm)
    {
        if (nota is < 1 or > 5) throw new ArgumentOutOfRangeException(nameof(nota));

        Nota = nota;
        Comentario = string.IsNullOrWhiteSpace(comentario) ? null : comentario.Trim();
        SolicitanteId = solicitanteId;
        CriadaEm = criadaEm;
    }
}
