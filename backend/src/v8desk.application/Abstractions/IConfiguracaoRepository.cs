using v8desk.domain.Entities;

namespace v8desk.application.Abstractions;

public interface IConfiguracaoRepository
{
    Task<Empresa?> ObterEmpresaAsync(CancellationToken cancellationToken = default);
    Task<Setor?> ObterSetorAsync(Guid id, CancellationToken cancellationToken = default);
    void Adicionar(Setor setor);
}
