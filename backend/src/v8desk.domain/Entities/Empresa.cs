namespace v8desk.domain.Entities;

public sealed class Empresa
{
    public Guid Id { get; }
    public string Nome { get; private set; }
    public CalendarioEmpresa Calendario { get; private set; }
    public PoliticaCicloVida PadraoCicloVida { get; private set; } = PoliticaCicloVida.Padrao;
    public int? LimiteSetoresPorAtendente { get; private set; }

    public Empresa(string nome, CalendarioEmpresa calendario)
    {
        ArgumentNullException.ThrowIfNull(calendario);
        Id = Guid.CreateVersion7();
        Nome = Guarda.Texto(nome, "o nome da empresa");
        Calendario = calendario;
    }

    public void Renomear(string nome)
    {
        Nome = Guarda.Texto(nome, "o nome da empresa");
    }

    public Setor CriarSetor(string nome) => new(Id, nome);

    public void ConfigurarCalendario(CalendarioEmpresa calendario)
    {
        ArgumentNullException.ThrowIfNull(calendario);
        Calendario = calendario;
    }

    public void ConfigurarCicloVida(PoliticaCicloVida politica)
    {
        ArgumentNullException.ThrowIfNull(politica);
        PadraoCicloVida = politica;
    }

    public void ConfigurarLimiteAtuacao(int? limite)
    {
        if (limite is < 1)
            throw new RegraNegocioException("O limite de setores por atendente deve ser de pelo menos 1.");

        LimiteSetoresPorAtendente = limite;
    }

    public void ValidarNovoVinculoAtendimento(Usuario usuario, int setoresAtendidos)
    {
        ArgumentNullException.ThrowIfNull(usuario);

        if (usuario.EmpresaId != Id)
            throw new RegraNegocioException("O usuário não pertence a esta empresa.");

        if (!usuario.Ativo)
            throw new RegraNegocioException("O usuário está inativo.");

        if (setoresAtendidos < 0)
            throw new RegraNegocioException("A quantidade de setores atendidos não pode ser negativa.");

        if (LimiteSetoresPorAtendente is { } limite && setoresAtendidos >= limite)
            throw new RegraNegocioException($"O atendente já atingiu o limite de {limite} setores de atuação.");
    }
}
