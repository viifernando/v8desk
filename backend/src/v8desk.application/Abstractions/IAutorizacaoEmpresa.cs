namespace v8desk.application.Abstractions;

// Capacidade administrativa emitida pelo host autenticado; nunca recebida no corpo do comando.
public interface IAutorizacaoEmpresa
{
    Task ExigirAdministracaoAsync(Guid empresaId, Guid usuarioId, CancellationToken cancellationToken = default);
}
