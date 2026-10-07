namespace v8desk.domain.Entities;

public sealed class Categoria
{
#pragma warning disable CS8618 // Materialização: valores preenchidos pelo EF.
    private Categoria() { }
#pragma warning restore CS8618

    private readonly List<PreQualificacao> _preQualificacoes = new();
    private readonly List<Categoria> _filhas = new();
    public Categoria? Pai { get; private set; }
    public Guid? CategoriaPaiId => Pai?.Id;
    public IReadOnlyList<Categoria> Subcategorias => _filhas.AsReadOnly();
    public bool PermiteChamadosComSubcategorias { get; private set; } = true;
    public bool PodeReceberChamado => Ativa && (Pai?.HierarquiaAtiva ?? true)
        && (PermiteChamadosComSubcategorias || !_filhas.Any(c => c.Ativa));
    private bool HierarquiaAtiva => Ativa && (Pai?.HierarquiaAtiva ?? true);

    internal void DefinirPai(Categoria? pai)
    {
        if (pai is not null && (pai.SetorId != SetorId || !pai.HierarquiaAtiva))
            throw new RegraNegocioException("Categoria pai deve estar ativa e pertencer ao mesmo setor.");
        for (var atual = pai; atual is not null; atual = atual.Pai)
            if (atual.Id == Id) throw new RegraNegocioException("A hierarquia não permite ciclos.");
        Pai?._filhas.Remove(this);
        Pai = pai;
        pai?._filhas.Add(this);
    }

    internal void ConfigurarRecebimento(bool permitir) => PermiteChamadosComSubcategorias = permitir;

    public void ExigirRecebimento()
    {
        if (!PodeReceberChamado)
            throw new RegraNegocioException("Selecione uma categoria ativa habilitada para receber chamados.");
    }

    public CaminhoCategoria CriarReferenciaHistorica()
    {
        var caminho = new List<ReferenciaHistorica>();
        for (var atual = this; atual is not null; atual = atual.Pai)
            caminho.Add(new(atual.Id, atual.Nome));
        caminho.Reverse();
        return new CaminhoCategoria(caminho);
    }

    public Guid? ObterFilaDestinoHerdada() => FilaDestinoId ?? Pai?.ObterFilaDestinoHerdada();
    public IReadOnlyList<PreQualificacao> ObterPreQualificacoesEfetivas() =>
        _preQualificacoes.Count > 0 ? PreQualificacoes : Pai?.ObterPreQualificacoesEfetivas() ?? Array.Empty<PreQualificacao>();

    public Guid Id { get; private set; }
    public Guid SetorId { get; private set; }
    public Guid EmpresaId { get; private set; }
    public string Nome { get; private set; }
    public bool Ativa { get; private set; }
    public Guid? FilaDestinoId { get; private set; }
    public Visibilidade VisibilidadePadrao { get; private set; } = Visibilidade.CompartilhadoComSetor;
    public IReadOnlyList<PreQualificacao> PreQualificacoes => _preQualificacoes.AsReadOnly();

    internal Categoria(Guid empresaId, Guid setorId, string nome)
    {
        Id = Guid.CreateVersion7();
        SetorId = Guarda.Identificador(setorId, "o setor da categoria");
        EmpresaId = Guarda.Identificador(empresaId, "a empresa");
        Nome = Guarda.Texto(nome, "o nome da categoria", 200);
        Ativa = true;
    }

    public void Renomear(string nome)
    {
        Nome = Guarda.Texto(nome, "o nome da categoria", 200);
    }

    public void ConfigurarDestino(Fila? fila)
    {
        if (fila is null)
        {
            FilaDestinoId = null;
            return;
        }

        if (fila.SetorId != SetorId)
            throw new RegraNegocioException("A fila de destino deve pertencer ao mesmo setor da categoria.");

        if (!fila.Ativa)
            throw new RegraNegocioException("A fila de destino está inativa.");

        FilaDestinoId = fila.Id;
    }

    public void DefinirVisibilidadePadrao(Visibilidade visibilidade)
    {
        VisibilidadePadrao = Guarda.Definido(visibilidade, "a visibilidade padrão");
    }

    public void PreQualificar(Usuario usuario, DateTimeOffset agora)
    {
        ArgumentNullException.ThrowIfNull(usuario);

        if (usuario.EmpresaId != EmpresaId)
            throw new RegraNegocioException("Escolha um atendente da mesma empresa da categoria.");

        if (!usuario.Ativo)
            throw new RegraNegocioException("Somente usuários ativos podem ser pré-qualificados.");

        if (_preQualificacoes.Exists(p => p.UsuarioId == usuario.Id))
            throw new RegraNegocioException("O usuário já está pré-qualificado para esta categoria.");

        _preQualificacoes.Add(new PreQualificacao(usuario.Id, agora));
    }

    public void RemoverPreQualificacao(Guid usuarioId)
    {
        var preQualificacao = _preQualificacoes.Find(p => p.UsuarioId == usuarioId)
            ?? throw new RegraNegocioException("O usuário não está pré-qualificado para esta categoria.");

        _preQualificacoes.Remove(preQualificacao);
    }

    public bool PreferencialPara(Guid usuarioId) => ObterPreQualificacoesEfetivas().Any(p => p.UsuarioId == usuarioId);

    public bool PossuiPreQualificados() => ObterPreQualificacoesEfetivas().Count > 0;

    public void Desativar()
    {
        Ativa = false;
    }
}
