namespace v8desk.domain.Entities;

public sealed class Fila
{
    private readonly List<MembroFila> _membros = new();

    public Guid Id { get; }
    public Guid SetorId { get; }
    public string Nome { get; private set; }
    public bool Geral { get; }
    public bool Ativa { get; private set; }
    public AcessoRestritoFila RegraAcessoRestrito { get; private set; } = AcessoRestritoFila.IntegrantesSelecionados;
    public IReadOnlyList<MembroFila> Membros => _membros.AsReadOnly();

    internal Fila(Guid setorId, string nome, bool geral)
    {
        Id = Guid.CreateVersion7();
        SetorId = Guarda.Identificador(setorId, "o setor da fila");
        Nome = Guarda.Texto(nome, "o nome da fila");
        Geral = geral;
        Ativa = true;
    }

    public void Renomear(string nome)
    {
        Nome = Guarda.Texto(nome, "o nome da fila");
    }

    public void AdicionarMembro(VinculoSetor vinculo)
    {
        ArgumentNullException.ThrowIfNull(vinculo);

        if (!Ativa)
            throw new RegraNegocioException("Não é possível adicionar membros a uma fila inativa.");

        if (vinculo.SetorId != SetorId)
            throw new RegraNegocioException("O vínculo informado pertence a outro setor.");

        if (!vinculo.PossuiPapel(PapelSetor.Atendente))
            throw new RegraNegocioException("Somente atendentes do setor podem integrar a fila.");

        if (BuscarMembro(vinculo.UsuarioId) is not null)
            throw new RegraNegocioException("O usuário já é membro desta fila.");

        _membros.Add(new MembroFila(vinculo.Id, vinculo.UsuarioId));
    }

    public void RemoverMembro(Guid usuarioId)
    {
        _membros.Remove(ObterMembro(usuarioId));
    }

    public void ConfigurarAcessoRestrito(AcessoRestritoFila regra)
    {
        RegraAcessoRestrito = Guarda.Definido(regra, "a regra de acesso restrito");
    }

    public void AutorizarAcessoRestrito(Guid usuarioId)
    {
        ObterMembro(usuarioId).ConcederAcessoRestrito();
    }

    public void RevogarAcessoRestrito(Guid usuarioId)
    {
        ObterMembro(usuarioId).RevogarAcessoRestrito();
    }

    public bool PodeAtender(Guid usuarioId) => Ativa && BuscarMembro(usuarioId) is not null;

    public bool PodeAcessarRestrito(Guid usuarioId) =>
        Ativa && BuscarMembro(usuarioId) is { } membro && AcessaRestrito(membro);

    public bool TemAtendenteAutorizadoParaRestritos() => Ativa && _membros.Exists(AcessaRestrito);

    public void Desativar()
    {
        if (Geral)
            throw new RegraNegocioException("A fila geral só pode ser desativada junto com o setor.");

        Ativa = false;
    }

    internal void DesativarComSetor()
    {
        Ativa = false;
    }

    private bool AcessaRestrito(MembroFila membro) =>
        RegraAcessoRestrito == AcessoRestritoFila.TodosOsIntegrantes || membro.AutorizadoParaRestritos;

    private MembroFila? BuscarMembro(Guid usuarioId) => _membros.Find(m => m.UsuarioId == usuarioId);

    private MembroFila ObterMembro(Guid usuarioId) =>
        BuscarMembro(usuarioId) ?? throw new RegraNegocioException("O usuário não é membro desta fila.");
}
