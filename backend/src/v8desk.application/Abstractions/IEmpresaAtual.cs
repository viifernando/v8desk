namespace v8desk.application.Abstractions;

// Resolvido a partir da identidade autenticada ou do contexto explícito de um job.
// Nunca usar diretamente um header/ID informado pelo cliente como autorização.
public interface IEmpresaAtual
{
    Guid EmpresaId { get; }
}
