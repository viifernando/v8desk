namespace v8desk.domain.Entities;

public sealed class Setor
{
#pragma warning disable CS8618 // Materialização: valores preenchidos pelo EF.
    private Setor() { }
#pragma warning restore CS8618

    private readonly List<Fila> _filas = new();
    private readonly List<Categoria> _categorias = new();

    public Guid Id { get; private set; }
    public Guid EmpresaId { get; private set; }
    public string Nome { get; private set; }
    public bool Ativo { get; private set; }
    public Guid FilaGeralId { get; private set; }
    public PoliticaSla Sla { get; private set; }
    public PoliticaCicloVida? CicloVidaEspecifico { get; private set; }
    public IReadOnlyList<Fila> Filas => _filas.AsReadOnly();
    public IReadOnlyList<Categoria> Categorias => _categorias.AsReadOnly();
    public Fila FilaGeral => _filas.First(f => f.Id == FilaGeralId);

    internal Setor(Guid empresaId, string nome)
    {
        Id = Guid.CreateVersion7();
        EmpresaId = Guarda.Identificador(empresaId, "a empresa do setor");
        Nome = Guarda.Texto(nome, "o nome do setor", 200);
        Ativo = true;
        Sla = new PoliticaSla(EmpresaId);

        var filaGeral = new Fila(EmpresaId, Id, "Geral", geral: true);
        _filas.Add(filaGeral);
        FilaGeralId = filaGeral.Id;
    }

    public void Renomear(string nome)
    {
        Nome = Guarda.Texto(nome, "o nome do setor", 200);
    }

    public Fila CriarFila(string nome)
    {
        GarantirAtivo();
        var nomeValidado = Guarda.Texto(nome, "o nome da fila", 200);

        if (_filas.Exists(f => f.Ativa && string.Equals(f.Nome, nomeValidado, StringComparison.OrdinalIgnoreCase)))
            throw new RegraNegocioException("Já existe uma fila ativa com este nome no setor.");

        var fila = new Fila(EmpresaId, Id, nomeValidado, geral: false);
        _filas.Add(fila);
        return fila;
    }

    public Categoria CriarCategoria(string nome, Categoria? pai = null)
    {
        GarantirAtivo();
        if (pai is not null) ExigirCategoriaDoSetor(pai);
        var nomeValidado = Guarda.Texto(nome, "o nome da categoria", 200);

        if (_categorias.Exists(c => c.Ativa && c.CategoriaPaiId == pai?.Id && string.Equals(c.Nome, nomeValidado, StringComparison.OrdinalIgnoreCase)))
            throw new RegraNegocioException("Já existe uma categoria ativa com este nome no setor.");

        var categoria = new Categoria(EmpresaId, Id, nomeValidado);
        categoria.DefinirPai(pai);
        _categorias.Add(categoria);
        return categoria;
    }

    public void MoverCategoria(Categoria categoria, Categoria? novoPai)
    {
        GarantirAtivo();
        ExigirCategoriaDoSetor(categoria);
        if (novoPai is not null) ExigirCategoriaDoSetor(novoPai);
        if (_categorias.Any(c => c.Id != categoria.Id && c.Ativa && c.CategoriaPaiId == novoPai?.Id
            && string.Equals(c.Nome, categoria.Nome, StringComparison.OrdinalIgnoreCase)))
            throw new RegraNegocioException("Já existe categoria com esse nome no nível de destino.");
        categoria.DefinirPai(novoPai);
    }

    public void RenomearCategoria(Categoria categoria, string nome)
    {
        GarantirAtivo();
        ExigirCategoriaDoSetor(categoria);
        var validado = Guarda.Texto(nome, "o nome da categoria", 200);
        if (categoria.Ativa && _categorias.Any(c => c.Id != categoria.Id && c.Ativa &&
            c.CategoriaPaiId == categoria.CategoriaPaiId && string.Equals(c.Nome, validado, StringComparison.OrdinalIgnoreCase)))
            throw new RegraNegocioException("Já existe categoria ativa com esse nome no mesmo nível.");
        categoria.Renomear(validado);
    }

    public void RenomearFila(Fila fila, string nome)
    {
        GarantirAtivo();
        if (!_filas.Contains(fila)) throw new RegraNegocioException("Escolha uma fila deste setor.");
        var validado = Guarda.Texto(nome, "o nome da fila", 200);
        if (fila.Ativa && _filas.Any(f => f.Id != fila.Id && f.Ativa && string.Equals(f.Nome, validado, StringComparison.OrdinalIgnoreCase)))
            throw new RegraNegocioException("Já existe uma fila ativa com este nome no setor.");
        fila.Renomear(validado);
    }

    public void ReativarCategoria(Categoria categoria)
    {
        GarantirAtivo();
        ExigirCategoriaDoSetor(categoria);
        if (_categorias.Any(c => c.Id != categoria.Id && c.Ativa && c.CategoriaPaiId == categoria.CategoriaPaiId
            && string.Equals(c.Nome, categoria.Nome, StringComparison.OrdinalIgnoreCase)))
            throw new RegraNegocioException("Já existe categoria ativa com esse nome no mesmo nível.");
        categoria.Reativar();
    }

    public void ConfigurarRecebimentoCategoria(Categoria categoria, bool permitirComSubcategorias)
    {
        GarantirAtivo();
        ExigirCategoriaDoSetor(categoria);
        categoria.ConfigurarRecebimento(permitirComSubcategorias);
    }

    private void ExigirCategoriaDoSetor(Categoria categoria)
    {
        ArgumentNullException.ThrowIfNull(categoria);
        if (!_categorias.Contains(categoria))
            throw new RegraNegocioException("A categoria não pertence a este setor.");
    }

    public void ConfigurarSla(PoliticaSla politica)
    {
        ArgumentNullException.ThrowIfNull(politica);
        if (politica.EmpresaId != EmpresaId)
            throw new RegraNegocioException("Escolha uma política de atendimento da mesma empresa do setor.");
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

        GarantirAtivo();
        ExigirCategoriaDoSetor(categoria);
        categoria.ExigirRecebimento();
        if (!categoria.Ativa)
            throw new RegraNegocioException("A categoria informada está inativa.");

        if (categoria.ObterFilaDestinoHerdada() is not { } destinoId)
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
