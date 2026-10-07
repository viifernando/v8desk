namespace v8desk.domain.Entities;

public sealed class MembroFila
{
#pragma warning disable CS8618 // Materialização: valores preenchidos pelo EF.
    private MembroFila() { }
#pragma warning restore CS8618

    public VinculoSetor Vinculo { get; private set; }
    public Guid VinculoSetorId => Vinculo.Id;
    public bool Habilitado => Vinculo.PossuiPapel(PapelSetor.Atendente);
    public Guid UsuarioId { get; private set; }
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
