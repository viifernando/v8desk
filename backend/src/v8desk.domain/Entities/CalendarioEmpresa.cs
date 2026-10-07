namespace v8desk.domain.Entities;

public sealed class CalendarioEmpresa
{
    public const int LimiteDiasBusca = 3660;

    private readonly Dictionary<DayOfWeek, IReadOnlyList<IntervaloExpediente>> _expediente = new();
    private readonly Dictionary<DateOnly, ExcecaoCalendario> _excecoes = new();

    public Guid Id { get; private init; } = Guid.CreateVersion7();
    private bool _snapshot;

    public CalendarioEmpresa CriarSnapshot()
    {
        if (_snapshot) return this;
        var copia = new CalendarioEmpresa(FusoHorarioId) { Id = Id, Versao = Versao, _snapshot = true };
        foreach (var item in _expediente) copia._expediente.Add(item.Key, item.Value);
        foreach (var item in _excecoes) copia._excecoes.Add(item.Key, item.Value);
        return copia;
    }

    private void ExigirEditavel()
    {
        if (_snapshot) throw new RegraNegocioException("Calendário histórico não pode ser alterado.");
    }
    public long Versao { get; private set; } = 1;
    public string FusoHorarioId { get; private set; }
    public IReadOnlyDictionary<DayOfWeek, IReadOnlyList<IntervaloExpediente>> Expediente => _expediente.AsReadOnly();
    public IReadOnlyCollection<ExcecaoCalendario> Excecoes => _excecoes.Values.OrderBy(excecao => excecao.Data).ToList().AsReadOnly();

    public CalendarioEmpresa(string fusoHorarioId)
    {
        FusoHorarioId = ValidarFusoHorario(fusoHorarioId);
    }

    public void AlterarFusoHorario(string fusoHorarioId)
    {
        ExigirEditavel();
        FusoHorarioId = ValidarFusoHorario(fusoHorarioId);
        Versao++;
    }

    public void DefinirExpediente(DayOfWeek dia, IReadOnlyCollection<IntervaloExpediente> intervalos)
    {
        ExigirEditavel();
        Guarda.Definido(dia, "o dia da semana");
        var normalizados = IntervaloExpediente.Normalizar(intervalos);

        if (normalizados.Count == 0)
            _expediente.Remove(dia);
        else
            _expediente[dia] = normalizados;

        Versao++;
    }

    public void RegistrarExcecao(ExcecaoCalendario excecao)
    {
        ExigirEditavel();
        ArgumentNullException.ThrowIfNull(excecao);

        _excecoes[excecao.Data] = excecao;
        Versao++;
    }

    public void RemoverExcecao(DateOnly data)
    {
        ExigirEditavel();
        if (!_excecoes.Remove(data))
            throw new RegraNegocioException("Não existe exceção de calendário para a data informada.");

        Versao++;
    }

    public IReadOnlyList<IntervaloExpediente> ObterIntervalos(DateOnly data)
    {
        if (_excecoes.TryGetValue(data, out var excecao))
            return excecao.Intervalos;

        if (_expediente.TryGetValue(data.DayOfWeek, out var intervalos))
            return intervalos;

        return [];
    }

    public bool PossuiExpediente(DateOnly data) => ObterIntervalos(data).Count > 0;

    public TimeSpan CalcularTempoUtil(DateTimeOffset inicio, DateTimeOffset fim)
    {
        if (fim <= inicio)
            return TimeSpan.Zero;

        var fuso = ObterFuso();
        var data = DataLocal(inicio, fuso);
        var dataFinal = DataLocal(fim, fuso);
        var total = TimeSpan.Zero;

        while (data <= dataFinal)
        {
            foreach (var intervalo in ObterIntervalos(data))
            {
                var inicioIntervalo = ParaInstante(data, intervalo.Inicio, fuso);
                var fimIntervalo = ParaInstante(data, intervalo.Fim, fuso);
                var inicioSobreposicao = inicioIntervalo > inicio ? inicioIntervalo : inicio;
                var fimSobreposicao = fimIntervalo < fim ? fimIntervalo : fim;

                if (fimSobreposicao > inicioSobreposicao)
                    total += fimSobreposicao - inicioSobreposicao;
            }

            data = data.AddDays(1);
        }

        return total;
    }

    public DateTimeOffset SomarHorasUteis(DateTimeOffset inicio, HorasUteis prazo)
    {
        ArgumentNullException.ThrowIfNull(prazo);

        var fuso = ObterFuso();
        var data = DataLocal(inicio, fuso);
        var restante = prazo.ParaTimeSpan();

        for (var dia = 0; dia < LimiteDiasBusca; dia++)
        {
            foreach (var intervalo in ObterIntervalos(data))
            {
                var inicioIntervalo = ParaInstante(data, intervalo.Inicio, fuso);
                var fimIntervalo = ParaInstante(data, intervalo.Fim, fuso);
                var inicioEfetivo = inicioIntervalo > inicio ? inicioIntervalo : inicio;

                if (fimIntervalo <= inicioEfetivo)
                    continue;

                var disponivel = fimIntervalo - inicioEfetivo;

                if (restante <= disponivel)
                    return inicioEfetivo + restante;

                restante -= disponivel;
            }

            data = data.AddDays(1);
        }

        throw new RegraNegocioException("Não foi possível calcular o prazo dentro do limite de dias do calendário.");
    }

    public DateTimeOffset SubtrairDiasUteis(DateTimeOffset limite, int dias)
    {
        if (dias < 0)
            throw new ArgumentOutOfRangeException(nameof(dias));

        if (dias == 0)
            return limite;

        var fuso = ObterFuso();
        var local = TimeZoneInfo.ConvertTime(limite, fuso).DateTime;
        var data = DateOnly.FromDateTime(local);
        var hora = TimeOnly.FromDateTime(local);
        var contados = 0;

        for (var dia = 0; dia < LimiteDiasBusca; dia++)
        {
            data = data.AddDays(-1);

            if (PossuiExpediente(data) && ++contados == dias)
                return ParaInstante(data, hora, fuso);
        }

        throw new RegraNegocioException("Não foi possível encontrar os dias úteis dentro do limite de dias do calendário.");
    }

    private TimeZoneInfo ObterFuso() => TimeZoneInfo.FindSystemTimeZoneById(FusoHorarioId);

    private static DateOnly DataLocal(DateTimeOffset instante, TimeZoneInfo fuso) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instante, fuso).DateTime);

    private static DateTimeOffset ParaInstante(DateOnly data, TimeOnly hora, TimeZoneInfo fuso)
    {
        var local = data.ToDateTime(hora);
        return new DateTimeOffset(local, fuso.GetUtcOffset(local));
    }

    private static string ValidarFusoHorario(string fusoHorarioId)
    {
        var id = Guarda.Texto(fusoHorarioId, "o fuso horário");

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id).Id;
        }
        catch (Exception excecao) when (excecao is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            throw new RegraNegocioException("Fuso horário inválido.");
        }
    }
}
