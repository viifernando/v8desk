namespace v8desk.domain.Entities;

public sealed class Categoria
{
    private readonly List<PreQualificacao> _preQualificacoes = new();

    public Guid Id { get; }
    public Guid SetorId { get; }
    public string Nome { get; private set; }
    public bool Ativa { get; private set; }
    public Guid? FilaDestinoId { get; private set; }
    public Visibilidade VisibilidadePadrao { get; private set; } = Visibilidade.CompartilhadoComSetor;
    public IReadOnlyList<PreQualificacao> PreQualificacoes => _preQualificacoes.AsReadOnly();

    internal Categoria(Guid setorId, string nome)
    {
        Id = Guid.CreateVersion7();
        SetorId = Guarda.Identificador(setorId, "o setor da categoria");
        Nome = Guarda.Texto(nome, "o nome da categoria");
        Ativa = true;
    }

    public void Renomear(string nome)
    {
        Nome = Guarda.Texto(nome, "o nome da categoria");
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

        FilaDestinoId = fila.Geral ? null : fila.Id;
    }

    public void DefinirVisibilidadePadrao(Visibilidade visibilidade)
    {
        VisibilidadePadrao = Guarda.Definido(visibilidade, "a visibilidade padrão");
    }

    public void PreQualificar(Usuario usuario, DateTimeOffset agora)
    {
        ArgumentNullException.ThrowIfNull(usuario);

        if (!usuario.Ativo)
            throw new RegraNegocioException("Somente usuários ativos podem ser pré-qualificados.");

        if (PreferencialPara(usuario.Id))
            throw new RegraNegocioException("O usuário já está pré-qualificado para esta categoria.");

        _preQualificacoes.Add(new PreQualificacao(usuario.Id, agora));
    }

    public void RemoverPreQualificacao(Guid usuarioId)
    {
        var preQualificacao = _preQualificacoes.Find(p => p.UsuarioId == usuarioId)
            ?? throw new RegraNegocioException("O usuário não está pré-qualificado para esta categoria.");

        _preQualificacoes.Remove(preQualificacao);
    }

    public bool PreferencialPara(Guid usuarioId) => _preQualificacoes.Exists(p => p.UsuarioId == usuarioId);

    public bool PossuiPreQualificados() => _preQualificacoes.Count > 0;

    public void Desativar()
    {
        Ativa = false;
    }
}
