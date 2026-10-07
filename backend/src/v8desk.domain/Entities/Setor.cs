namespace v8desk.domain.Entities;

public sealed class Setor
{
    private readonly List<Fila> _filas = new();
    private readonly List<Categoria> _categorias = new();

    public Guid Id { get; }
    public Guid EmpresaId { get; }
    public string Nome { get; private set; }
    public bool Ativo { get; private set; }
    public Guid FilaGeralId { get; }
    public PoliticaSla Sla { get; private set; }
    public PoliticaCicloVida? CicloVidaEspecifico { get; private set; }
    public IReadOnlyList<Fila> Filas => _filas.AsReadOnly();
    public IReadOnlyList<Categoria> Categorias => _categorias.AsReadOnly();
    public Fila FilaGeral => _filas.First(f => f.Id == FilaGeralId);

    internal Setor(Guid empresaId, string nome)
    {
        Id = Guid.CreateVersion7();
        EmpresaId = Guarda.Identificador(empresaId, "a empresa do setor");
        Nome = Guarda.Texto(nome, "o nome do setor");
        Ativo = true;
        Sla = new PoliticaSla();

        var filaGeral = new Fila(Id, "Geral", geral: true);
        _filas.Add(filaGeral);
        FilaGeralId = filaGeral.Id;
    }

    public void Renomear(string nome)
    {
        Nome = Guarda.Texto(nome, "o nome do setor");
    }

    public Fila CriarFila(string nome)
    {
        GarantirAtivo();
        var nomeValidado = Guarda.Texto(nome, "o nome da fila");

        if (_filas.Exists(f => f.Ativa && string.Equals(f.Nome, nomeValidado, StringComparison.OrdinalIgnoreCase)))
            throw new RegraNegocioException("Já existe uma fila ativa com este nome no setor.");

        var fila = new Fila(Id, nomeValidado, geral: false);
        _filas.Add(fila);
        return fila;
    }

    public Categoria CriarCategoria(string nome)
    {
        GarantirAtivo();
        var nomeValidado = Guarda.Texto(nome, "o nome da categoria");

        if (_categorias.Exists(c => c.Ativa && string.Equals(c.Nome, nomeValidado, StringComparison.OrdinalIgnoreCase)))
            throw new RegraNegocioException("Já existe uma categoria ativa com este nome no setor.");

        var categoria = new Categoria(Id, nomeValidado);
        _categorias.Add(categoria);
        return categoria;
    }

    public void ConfigurarSla(PoliticaSla politica)
    {
        ArgumentNullException.ThrowIfNull(politica);
        politica.ValidarCobertura();
        Sla = politica;
    }

    public void ConfigurarCicloVida(PoliticaCicloVida? substituicao)
    {
        CicloVidaEspecifico = substituicao;
    }

    public PoliticaCicloVida ObterCicloVida(PoliticaCicloVida padraoEmpresa) => CicloVidaEspecifico ?? padraoEmpresa;

    public Guid DeterminarFilaInicial(Categoria categoria)
    {
        ArgumentNullException.ThrowIfNull(categoria);

        if (categoria.SetorId != Id)
            throw new RegraNegocioException("A categoria informada não pertence a este setor.");

        if (!categoria.Ativa)
            throw new RegraNegocioException("A categoria informada está inativa.");

        if (categoria.FilaDestinoId is not { } destinoId)
            return FilaGeralId;

        return _filas.Find(f => f.Id == destinoId) is { Ativa: true } destino ? destino.Id : FilaGeralId;
    }

    public void Desativar()
    {
        Ativo = false;
        _filas.ForEach(f => f.DesativarComSetor());
        _categorias.ForEach(c => c.Desativar());
    }

    private void GarantirAtivo()
    {
        if (!Ativo)
            throw new RegraNegocioException("O setor está inativo.");
    }
}
