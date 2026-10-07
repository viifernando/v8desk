namespace v8desk.domain.ValueObjects;

public sealed class CaminhoCategoria
{
    public IReadOnlyList<ReferenciaHistorica> Niveis { get; }
    public Guid Id => Niveis[^1].Id;
    public string NomeNaOcorrencia => Niveis[^1].NomeNaOcorrencia;
    public string Caminho => string.Join(" → ", Niveis.Select(n => n.NomeNaOcorrencia));

    public CaminhoCategoria(IEnumerable<ReferenciaHistorica> niveis)
    {
        ArgumentNullException.ThrowIfNull(niveis);
        var lista = niveis.ToList();
        if (lista.Count == 0 || lista.Any(n => n is null) || lista.Select(n => n.Id).Distinct().Count() != lista.Count)
            throw new RegraNegocioException("O caminho deve conter categorias distintas e válidas.");
        Niveis = lista.AsReadOnly();
    }
}
