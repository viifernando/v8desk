namespace v8desk.domain.Interfaces;

public interface IAutorizacaoChamado
{
    bool PodeVisualizar(Chamado chamado, Guid usuarioId);
    void ExigirAtendimento(Chamado chamado, Guid usuarioId);
    void ExigirTransferencia(Chamado chamado, Guid usuarioId, Fila destino);
    void ExigirCancelamento(Chamado chamado, Guid usuarioId);
    void ExigirGestaoSetor(Guid usuarioId, Guid setorId);
}
