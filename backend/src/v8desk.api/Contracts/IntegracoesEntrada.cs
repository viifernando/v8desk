using System.ComponentModel.DataAnnotations;
using v8desk.application.Integracoes;
using v8desk.domain.ValueObjects;

namespace v8desk.api.Contracts;

public sealed record EmailIntegracaoEntrada
{
    [EnumDataType(typeof(ProvedorEmail))] public required ProvedorEmail Provedor { get; init; }
    [Required(ErrorMessage = "Informe o e-mail remetente."), StringLength(254)] public required string Remetente { get; init; }
    [Required(ErrorMessage = "Informe o nome do remetente."), StringLength(200)] public required string NomeRemetente { get; init; }
    [StringLength(253)] public string? HostSmtp { get; init; }
    [Range(1, 65535)] public int PortaSmtp { get; init; } = 587;
    [EnumDataType(typeof(SegurancaSmtp))] public SegurancaSmtp SegurancaSmtp { get; init; }
    [StringLength(254)] public string? UsuarioSmtp { get; init; }
    [StringLength(4096)] public string? Segredo { get; init; }
    [IdValido] public Guid? TenantGraphId { get; init; }
    [IdValido] public Guid? ClienteGraphId { get; init; }
    public DateTimeOffset? CredencialExpiraEm { get; init; }
    public ConfigurarEmail ParaComando() => new(Provedor, Remetente, NomeRemetente, HostSmtp, PortaSmtp,
        SegurancaSmtp, UsuarioSmtp, Segredo, TenantGraphId, ClienteGraphId, CredencialExpiraEm);
}
public sealed record MicrosoftIntegracaoEntrada
{
    [IdValido] public required Guid TenantId { get; init; }
    [IdValido] public required Guid ApiClienteId { get; init; }
    [IdValido] public required Guid ClienteLoginId { get; init; }
}
public sealed record VinculoMicrosoftEntrada
{
    [IdValido] public required Guid ObjetoId { get; init; }
    public required bool Administrador { get; init; }
}
public sealed record DesvinculoMicrosoftEntrada { [IdValido] public required Guid ObjetoId { get; init; } }
public sealed record ContatoNotificacaoEntrada
{
    [Required(ErrorMessage = "Informe o e-mail de notificações."), StringLength(254)] public required string Email { get; init; }
}
public sealed record AtivacaoIntegracaoEntrada { public required bool Ativo { get; init; } }
