namespace v8desk.application.Abstractions;

public sealed record EntregaOutbox(Guid Id, Guid EmpresaId, string Tipo, string Payload);

public interface IPublicadorOutbox
{
    // Entrega pelo menos uma vez: deduplicar pelo par EmpresaId/Id no destino.
    Task PublicarAsync(EntregaOutbox mensagem, CancellationToken cancellationToken);
}
