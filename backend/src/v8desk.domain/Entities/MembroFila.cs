namespace v8desk.domain.Entities;

public sealed class MembroFila
{
    public Guid VinculoSetorId { get; }
    public Guid UsuarioId { get; }
    public bool AutorizadoParaRestritos { get; private set; }

    internal MembroFila(Guid vinculoSetorId, Guid usuarioId)
    {
        VinculoSetorId = Guarda.Identificador(vinculoSetorId, "o vínculo com o setor");
        UsuarioId = Guarda.Identificador(usuarioId, "o usuário");
    }

    internal void ConcederAcessoRestrito()
    {
        AutorizadoParaRestritos = true;
    }

    internal void RevogarAcessoRestrito()
    {
        AutorizadoParaRestritos = false;
    }
}
