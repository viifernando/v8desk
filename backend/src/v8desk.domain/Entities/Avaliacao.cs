namespace v8desk.domain.Entities;

public sealed class Avaliacao
{
    public const int NotaMinimaSemJustificativa = 3;

    public int Nota { get; }
    public string? Comentario { get; }
    public Guid SolicitanteId { get; }
    public DateTimeOffset CriadaEm { get; }

    public Avaliacao(int nota, string? comentario, Guid solicitanteId, DateTimeOffset criadaEm)
    {
        if (nota is < 1 or > 5)
            throw new ValidacaoDominioException("nota_invalida", "nota", "Escolha uma nota de 1 a 5.");
        if (ExigeJustificativa(nota) && string.IsNullOrWhiteSpace(comentario))
            throw new ValidacaoDominioException("justificativa_obrigatoria", "comentario",
                "Conte o que não foi bom para a equipe poder melhorar.");

        Nota = nota;
        Comentario = string.IsNullOrWhiteSpace(comentario) ? null : Guarda.Texto(comentario, "o comentário");
        SolicitanteId = Guarda.Identificador(solicitanteId, "o solicitante");
        CriadaEm = Guarda.Instante(criadaEm, "a data da avaliação");
    }

    public static bool ExigeJustificativa(int nota) => nota < NotaMinimaSemJustificativa;
}
