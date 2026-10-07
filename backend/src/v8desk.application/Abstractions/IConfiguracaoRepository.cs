using v8desk.domain.Entities;

namespace v8desk.application.Abstractions;

public interface IConfiguracaoRepository
{
    Task<Empresa?> ObterEmpresaAsync(CancellationToken cancellationToken = default);
    Task<Setor?> ObterSetorAsync(Guid id, CancellationToken cancellationToken = default);
    void Adicionar(Setor setor);
    Task<Usuario?> ObterUsuarioAsync(Guid id, CancellationToken cancellationToken = default);
    Task<VinculoSetor?> ObterVinculoAsync(Guid id, CancellationToken cancellationToken = default);
    void Adicionar(VinculoSetor vinculo);
    void Adicionar(Usuario usuario);
    // Serializa validações de limite por usuário dentro da transação do comando.
    Task BloquearVinculosUsuarioAsync(Guid usuarioId, CancellationToken cancellationToken = default);
    Task BloquearSetorAsync(Guid setorId, CancellationToken cancellationToken = default);
}
