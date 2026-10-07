using v8desk.application.Abstractions;
using v8desk.domain.Entities;
using v8desk.domain.Enums;
using v8desk.domain.Exceptions;
using v8desk.domain.Interfaces;

namespace v8desk.application.Seguranca;

public sealed class AutorizacaoChamado(Guid empresaId, DadosAcesso dados) : IAutorizacaoChamado
{
    public Usuario Usuario => dados.Usuario;
    public bool PossuiVinculo(Guid setorId) => Ativo && SetorAtivo(setorId) &&
        dados.Vinculos.Any(v => v.UsuarioId == Usuario.Id && v.Usuario.EmpresaId == empresaId && v.SetorId == setorId && v.Ativo && v.Usuario.Ativo);
    private bool Ativo => dados.Usuario.Ativo && dados.Usuario.EmpresaId == empresaId;
    private bool SetorAtivo(Guid id) => dados.Setores.Any(s => s.Id == id && s.Ativo);
    public bool SetorDisponivel(Guid id) => Ativo && SetorAtivo(id);
    public Fila Fila(Guid id) => dados.Filas.SingleOrDefault(f => f.Id == id && f.EmpresaId == empresaId)
        ?? throw new AcessoNegadoException("A fila não está disponível para esta operação.");

    public bool PodeVisualizar(ReferenciaAcessoChamado chamado) => Ativo && chamado.EmpresaId == empresaId &&
        (chamado.SolicitanteId == Usuario.Id ||
         (chamado.Visibilidade == Visibilidade.AcessoRestrito
             ? PodeAtender(chamado)
             : PossuiVinculo(chamado.SetorOrigemId) || PodeAtender(chamado)));

    public bool PodeAtender(ReferenciaAcessoChamado chamado) => Ativo && chamado.EmpresaId == empresaId &&
        SetorAtivo(chamado.SetorAtualId) && dados.Filas.Any(f => f.Id == chamado.FilaAtualId &&
            f.SetorId == chamado.SetorAtualId && f.EmpresaId == empresaId && f.PodeAtender(Usuario.Id) &&
            (chamado.Visibilidade != Visibilidade.AcessoRestrito || f.PodeAcessarRestrito(Usuario.Id)));

    public void ExigirVisualizacao(ReferenciaAcessoChamado chamado)
    {
        if (!PodeVisualizar(chamado)) Negar();
    }
    public void ExigirAtendimento(ReferenciaAcessoChamado chamado)
    {
        if (!PodeAtender(chamado)) Negar();
    }
    public void ExigirSolicitante(ReferenciaAcessoChamado chamado)
    {
        ExigirVisualizacao(chamado);
        if (chamado.SolicitanteId != Usuario.Id) Negar();
    }
    public void ExigirCancelamento(ReferenciaAcessoChamado chamado)
    {
        ExigirVisualizacao(chamado);
        if (chamado.SolicitanteId != Usuario.Id) ExigirAtendimento(chamado);
    }
    private bool Gestor(Guid setorId) => Ativo && SetorAtivo(setorId) &&
        dados.Vinculos.Any(v => v.UsuarioId == Usuario.Id && v.Usuario.EmpresaId == empresaId && v.SetorId == setorId && v.PossuiPapel(PapelSetor.Gestor));

    public void ExigirGestaoSetor(Guid usuarioId, Guid setorId)
    {
        if (usuarioId != Usuario.Id || !Gestor(setorId)) Negar();
    }
    public bool PodeVisualizar(Chamado chamado, Guid usuarioId) => usuarioId == Usuario.Id && PodeVisualizar(Referencia(chamado));
    public void ExigirAtendimento(Chamado chamado, Guid usuarioId)
    {
        if (usuarioId != Usuario.Id) Negar();
        ExigirAtendimento(Referencia(chamado));
    }
    public void ExigirTransferencia(Chamado chamado, Guid usuarioId, Fila destino)
    {
        ExigirAtendimento(chamado, usuarioId);
        if (destino.EmpresaId != empresaId || !destino.Ativa || !SetorAtivo(destino.SetorId)) Negar();
    }
    public void ExigirCancelamento(Chamado chamado, Guid usuarioId)
    {
        if (usuarioId != Usuario.Id) Negar();
        ExigirCancelamento(Referencia(chamado));
    }
    public static ReferenciaAcessoChamado Referencia(Chamado c) => new(c.Id, c.EmpresaId, c.SolicitanteId,
        c.SetorOrigemNaAbertura.Id, c.SetorAtualId, c.FilaAtualId, c.Visibilidade);
    private static void Negar() => throw new AcessoNegadoException("Você não tem permissão para realizar esta operação.");
}
