namespace v8desk.infrastructure.Persistence;

public sealed class OutboxMensagem
{
    public Guid Id { get; private set; }
    public Guid EmpresaId { get; private set; }
    public string Tipo { get; private set; } = string.Empty;
    public string Payload { get; private set; } = string.Empty;
    public DateTimeOffset CriadaEm { get; private set; }
    public DateTimeOffset? ProcessadaEm { get; private set; }
    public DateTimeOffset DisponivelEm { get; private set; }
    public int Tentativas { get; private set; }
    public string? UltimoErro { get; private set; }

    private OutboxMensagem() { }
    internal OutboxMensagem(Guid id, Guid empresaId, string tipo, string payload, DateTimeOffset criadaEm)
    {
        Id = id; EmpresaId = empresaId; Tipo = tipo; Payload = payload; CriadaEm = criadaEm; DisponivelEm = criadaEm;
    }
    internal void Confirmar(DateTimeOffset agora) { ProcessadaEm = agora; Tentativas++; UltimoErro = null; }
    internal void RegistrarFalha(DateTimeOffset agora)
    {
        Tentativas++;
        UltimoErro = "Falha na publicação. Consulte o log pelo identificador da mensagem.";
        DisponivelEm = agora.AddSeconds(Math.Min(3600, Math.Pow(2, Math.Min(Tentativas, 12))));
    }
}
