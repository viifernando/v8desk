namespace v8desk.domain.Entities;

public sealed class PoliticaSla
{
    private readonly List<MetaSla> _metas = new();

    public Guid Id { get; } = Guid.CreateVersion7();
    public long Versao { get; private set; } = 1;
    public decimal PercentualAlerta { get; private set; } = 80;
    public IReadOnlyList<MetaSla> Metas => _metas.AsReadOnly();

    public PoliticaSla()
    {
    }

    public void DefinirMeta(Prioridade prioridade, TipoSla tipo, HorasUteis prazo)
    {
        Guarda.Definido(prioridade, "a prioridade");
        Guarda.Definido(tipo, "o tipo de SLA");
        ArgumentNullException.ThrowIfNull(prazo);

        _metas.RemoveAll(meta => meta.Prioridade == prioridade && meta.Tipo == tipo);
        _metas.Add(new MetaSla(prioridade, tipo, prazo));
        Versao++;
    }

    public void ConfigurarAlerta(decimal percentual)
    {
        if (percentual <= 0 || percentual >= 100)
            throw new RegraNegocioException("O percentual de alerta deve ser maior que 0 e menor que 100.");

        PercentualAlerta = percentual;
        Versao++;
    }

    public MetaSla ObterMeta(Prioridade prioridade, TipoSla tipo) =>
        _metas.FirstOrDefault(meta => meta.Prioridade == prioridade && meta.Tipo == tipo)
        ?? throw new RegraNegocioException($"Não existe meta de SLA para a prioridade {prioridade} e o tipo {tipo}.");

    public void ValidarCobertura()
    {
        var faltantes = Enum.GetValues<Prioridade>()
            .SelectMany(prioridade => Enum.GetValues<TipoSla>().Select(tipo => (Prioridade: prioridade, Tipo: tipo)))
            .Where(par => !_metas.Any(meta => meta.Prioridade == par.Prioridade && meta.Tipo == par.Tipo))
            .Select(par => $"{par.Prioridade}/{par.Tipo}")
            .ToList();

        if (faltantes.Count > 0)
            throw new RegraNegocioException($"A política de SLA não possui metas para: {string.Join(", ", faltantes)}.");
    }
}
