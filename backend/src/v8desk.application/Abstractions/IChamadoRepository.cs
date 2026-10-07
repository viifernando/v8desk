using v8desk.domain.Entities;

namespace v8desk.application.Abstractions;

public interface IChamadoRepository
{
    Task<Chamado?> ObterParaAtualizacaoAsync(Guid id, CancellationToken cancellationToken = default, Guid? mensagemId = null);
    void Adicionar(Chamado chamado);
}

public interface IUnidadeTrabalho
{
    Task<int> SalvarAsync(CancellationToken cancellationToken = default);
}
