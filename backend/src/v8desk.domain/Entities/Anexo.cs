namespace v8desk.domain.Entities;

public sealed record Anexo
{
    public Guid Id { get; }
    public string NomeOriginal { get; }
    public string TipoConteudo { get; }
    public long TamanhoBytes { get; }
    public string ChaveArmazenamento { get; }
    public string HashIntegridade { get; }
    public Guid AutorId { get; }
    public DateTimeOffset CriadoEm { get; }

    public Anexo(string nomeOriginal, string tipoConteudo, long tamanhoBytes,
        string chaveArmazenamento, string hashIntegridade, Guid autorId, DateTimeOffset criadoEm)
    {
        if (tamanhoBytes <= 0)
            throw new RegraNegocioException("O anexo não pode estar vazio.");

        Id = Guid.CreateVersion7();
        NomeOriginal = Guarda.Texto(nomeOriginal, "o nome do arquivo");
        TipoConteudo = Guarda.Texto(tipoConteudo, "o tipo de conteúdo");
        TamanhoBytes = tamanhoBytes;
        ChaveArmazenamento = Guarda.Texto(chaveArmazenamento, "a chave de armazenamento");
        HashIntegridade = Guarda.Texto(hashIntegridade, "o hash de integridade");
        AutorId = Guarda.Identificador(autorId, "o autor do anexo");
        CriadoEm = criadoEm;
    }
}
