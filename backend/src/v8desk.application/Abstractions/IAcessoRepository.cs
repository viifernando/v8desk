using v8desk.domain.Entities;
using v8desk.domain.Enums;

namespace v8desk.application.Abstractions;

public sealed record ReferenciaAcessoChamado(Guid Id, Guid EmpresaId, Guid SolicitanteId,
    Guid SetorOrigemId, Guid SetorAtualId, Guid FilaAtualId, Visibilidade Visibilidade);
public sealed record SetorAcesso(Guid Id, bool Ativo);
public sealed record DadosAcesso(Usuario Usuario, IReadOnlyList<VinculoSetor> Vinculos,
    IReadOnlyList<SetorAcesso> Setores, IReadOnlyList<Fila> Filas);

// Leituras sem tracking. Uma referência de segurança não é um agregado parcialmente carregado.
public interface IAcessoRepository
{
    Task<ReferenciaAcessoChamado?> ObterReferenciaAsync(Guid chamadoId, CancellationToken cancellationToken = default);
    Task<DadosAcesso?> ObterDadosAsync(Guid usuarioId, IReadOnlyCollection<Guid> filas,
        CancellationToken cancellationToken = default);
    Task<Usuario?> ObterUsuarioAsync(Guid id, CancellationToken cancellationToken = default);
}
