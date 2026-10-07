namespace v8desk.domain.Entities;

public sealed class VinculoSetor
{
    private readonly HashSet<PapelSetor> _papeis = new();

    public Guid Id { get; }
    public Usuario Usuario { get; }
    public Guid UsuarioId => Usuario.Id;
    public Guid SetorId { get; }
    public bool Ativo { get; private set; }
    public IReadOnlyCollection<PapelSetor> Papeis => _papeis.ToList().AsReadOnly();

    public VinculoSetor(Usuario usuario, Guid setorId, PapelSetor papelInicial)
    {
        Id = Guid.CreateVersion7();
        ArgumentNullException.ThrowIfNull(usuario);
        Usuario = usuario;
        SetorId = Guarda.Identificador(setorId, "o setor do vínculo");
        _papeis.Add(Guarda.Definido(papelInicial, "o papel no setor"));
        Ativo = true;
    }

    public bool PossuiPapel(PapelSetor papel) => Ativo && Usuario.Ativo && _papeis.Contains(papel);

    public void ConcederPapel(PapelSetor papel)
    {
        GarantirAtivo();
        _papeis.Add(Guarda.Definido(papel, "o papel no setor"));
    }

    public void RevogarPapel(PapelSetor papel)
    {
        GarantirAtivo();

        if (!_papeis.Contains(papel))
            return;

        if (_papeis.Count == 1)
            throw new RegraNegocioException("O vínculo precisa manter ao menos um papel no setor.");

        _papeis.Remove(papel);
    }

    public void Desativar()
    {
        Ativo = false;
    }

    private void GarantirAtivo()
    {
        if (!Ativo)
            throw new RegraNegocioException("O vínculo com o setor está inativo.");
    }
}
