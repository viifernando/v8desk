namespace v8desk.domain.Entities;

public sealed class PoliticaSla
{
#pragma warning disable CS8618 // Materialização: valores preenchidos pelo EF.
#pragma warning restore CS8618

    private readonly List<MetaSla> _metas = new();

    public Guid Id { get; private set; } = Guid.CreateVersion7();
    public Guid EmpresaId { get; private set; }
    public long Versao { get; private set; } = 1;
    public decimal PercentualAlerta { get; private set; } = 80;
    public IReadOnlyList<MetaSla> Metas => _metas.AsReadOnly();

    private PoliticaSla() { }

    public PoliticaSla(Guid empresaId)
    {
        EmpresaId = Guarda.Identificador(empresaId, "a empresa da política de atendimento");
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
            throw new ValidacaoDominioException("percentual_invalido", "percentualAlerta", "Informe um percentual de alerta maior que 0 e menor que 100.");

        PercentualAlerta = percentual;
        Versao++;
    }

    public MetaSla ObterMeta(Prioridade prioridade, TipoSla tipo)
    {
        Guarda.Definido(prioridade, "a prioridade");
        Guarda.Definido(tipo, "o tipo de prazo");
        return _metas.FirstOrDefault(meta => meta.Prioridade == prioridade && meta.Tipo == tipo)
            ?? throw new RegraNegocioException("O prazo de atendimento ainda não foi configurado para esta prioridade. Peça ao gestor do setor para completar a configuração.");
    }

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
