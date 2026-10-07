namespace v8desk.domain.Entities;

public sealed class Avaliacao
{
    public int Nota { get; }
    public string? Comentario { get; }
    public Guid SolicitanteId { get; }
    public DateTimeOffset CriadaEm { get; }

    public Avaliacao(int nota, string? comentario, Guid solicitanteId, DateTimeOffset criadaEm)
    {
        if (nota is < 1 or > 5)
            throw new ValidacaoDominioException("nota_invalida", "nota", "Escolha uma nota de 1 a 5.");

        Nota = nota;
        Comentario = string.IsNullOrWhiteSpace(comentario) ? null : Guarda.Texto(comentario, "o comentário");
        SolicitanteId = Guarda.Identificador(solicitanteId, "o solicitante");
        CriadaEm = Guarda.Instante(criadaEm, "a data da avaliação");
    }
}
