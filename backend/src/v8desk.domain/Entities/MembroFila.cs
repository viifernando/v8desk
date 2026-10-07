namespace v8desk.domain.Entities;

public sealed class MembroFila
{
    public VinculoSetor Vinculo { get; }
    public Guid VinculoSetorId => Vinculo.Id;
    public bool Habilitado => Vinculo.PossuiPapel(PapelSetor.Atendente);
    public Guid UsuarioId { get; }
    public bool AutorizadoParaRestritos { get; private set; }

    internal MembroFila(VinculoSetor vinculo)
    {
        Vinculo = vinculo;
        UsuarioId = vinculo.UsuarioId;
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
