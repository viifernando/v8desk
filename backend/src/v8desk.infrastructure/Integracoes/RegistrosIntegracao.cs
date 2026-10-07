namespace v8desk.infrastructure.Integracoes;

public sealed class VinculoMicrosoft
{
    public Guid Id { get; private set; }
    public Guid EmpresaId { get; private set; }
    public Guid UsuarioId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ObjetoId { get; private set; }
    public bool Administrador { get; private set; }
    public bool Ativo { get; private set; }
    private VinculoMicrosoft() { }
    internal VinculoMicrosoft(Guid empresa, Guid usuario, Guid tenant, Guid objeto, bool admin, bool ativo)
    { Id = Guid.CreateVersion7(); EmpresaId = empresa; UsuarioId = usuario; TenantId = tenant; Atualizar(objeto, admin, ativo); }
    internal void Atualizar(Guid objeto, bool admin, bool ativo) { ObjetoId = objeto; Administrador = admin; Ativo = ativo; }
}
public sealed class ContatoNotificacao
{
    public Guid Id { get; private set; }
    public Guid EmpresaId { get; private set; }
    public Guid UsuarioId { get; private set; }
    public string Email { get; private set; } = "";
    private ContatoNotificacao() { }
    internal ContatoNotificacao(Guid empresa, Guid usuario, string email) { Id = usuario; EmpresaId = empresa; UsuarioId = usuario; Atualizar(email); }
    internal void Atualizar(string email) => Email = email;
}
public sealed class AuditoriaIntegracao
{
    public Guid Id { get; private set; }
    public Guid EmpresaId { get; private set; }
    public Guid UsuarioId { get; private set; }
    public string Acao { get; private set; } = "";
    public DateTimeOffset OcorridaEm { get; private set; }
    private AuditoriaIntegracao() { }
    internal AuditoriaIntegracao(Guid empresa, Guid usuario, string acao, DateTimeOffset agora)
    { Id = Guid.CreateVersion7(); EmpresaId = empresa; UsuarioId = usuario; Acao = acao; OcorridaEm = agora; }
}
public sealed class EntregaEmail
{
    public Guid Id { get; private set; }
    public Guid EmpresaId { get; private set; }
    public Guid UsuarioId { get; private set; }
    public Guid? EventoId { get; private set; }
    public Guid? ChamadoId { get; private set; }
    public bool Teste { get; private set; }
    public long RevisaoConfiguracao { get; private set; }
    public string Situacao { get; private set; } = "Pendente";
    public int Tentativas { get; private set; }
    public DateTimeOffset CriadaEm { get; private set; }
    public DateTimeOffset? ProximaTentativaEm { get; private set; }
    public string? Codigo { get; private set; }
    private EntregaEmail() { }
    internal EntregaEmail(Guid id, Guid empresa, Guid usuario, Guid? evento, Guid? chamado, bool teste, long revisao, DateTimeOffset agora)
    { Id = id; EmpresaId = empresa; UsuarioId = usuario; EventoId = evento; ChamadoId = chamado; Teste = teste; RevisaoConfiguracao = revisao; CriadaEm = agora; ProximaTentativaEm = agora; }
    internal void Aceitar() { Situacao = "AceitaPeloProvedor"; Tentativas++; ProximaTentativaEm = null; Codigo = "aceita_provedor"; }
    internal void Ignorar(string codigo) { Situacao = "Ignorada"; Codigo = codigo; ProximaTentativaEm = null; }
    internal void Falhar(string codigo, bool temporaria, TimeSpan? espera, DateTimeOffset agora)
    {
        Tentativas++; Codigo = codigo;
        Situacao = temporaria && Tentativas < 10 ? "Pendente" : "Falha";
        ProximaTentativaEm = Situacao == "Pendente" ? agora.Add(espera is { } pausa
            ? TimeSpan.FromSeconds(Math.Clamp(pausa.TotalSeconds, 1, 86400)) : TimeSpan.FromSeconds(Math.Min(3600, Math.Pow(2, Tentativas)))) : null;
    }
    internal void Repetir(DateTimeOffset agora)
    {
        if (Situacao != "Falha") throw new v8desk.domain.Exceptions.RegraNegocioException("Somente entregas com falha podem ser reagendadas.");
        Situacao = "Pendente"; Tentativas = 0; Codigo = null; ProximaTentativaEm = agora;
    }
}
