namespace v8desk.application.Abstractions;

// Identidade verificada pelo host. Jobs precisam fornecer uma identidade explícita.
public interface IUsuarioAtual
{
    Guid UsuarioId { get; }
}
