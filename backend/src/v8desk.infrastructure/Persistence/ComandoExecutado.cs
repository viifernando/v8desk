namespace v8desk.infrastructure.Persistence;

public sealed class ComandoExecutado
{
    public Guid Id { get; private set; }
    public Guid EmpresaId { get; private set; }
    public Guid UsuarioId { get; private set; }
    public string Chave { get; private set; } = "";
    public string Hash { get; private set; } = "";
    public string Resultado { get; private set; } = "";
    public DateTimeOffset CriadoEm { get; private set; }
    private ComandoExecutado() { }
    internal ComandoExecutado(Guid empresa, Guid usuario, string chave, string hash, string resultado)
    {
        Id = Guid.CreateVersion7(); EmpresaId = empresa; UsuarioId = usuario; Chave = chave;
        Hash = hash; Resultado = resultado; CriadoEm = DateTimeOffset.UtcNow;
    }
}
