using v8desk.domain.Entities;
using v8desk.domain.ValueObjects;

namespace v8desk.application.Integracoes;

public sealed record ConfigurarEmail(ProvedorEmail Provedor, string Remetente, string NomeRemetente,
    string? HostSmtp, int PortaSmtp, SegurancaSmtp SegurancaSmtp, string? UsuarioSmtp, string? Segredo,
    Guid? TenantGraphId, Guid? ClienteGraphId, DateTimeOffset? CredencialExpiraEm);
public sealed record ConfigurarMicrosoft(Guid TenantId, Guid ApiClienteId, Guid ClienteLoginId);
public sealed record VincularMicrosoft(Guid UsuarioId, Guid ObjetoId, bool Administrador);
public sealed record ConfigurarContato(Guid UsuarioId, string Email);
public sealed record ResultadoIntegracao(Guid RegistroId, string Mensagem);
public sealed record EmailConfigurado(ProvedorEmail Provedor, string Remetente, string NomeRemetente,
    string? HostSmtp, int PortaSmtp, SegurancaSmtp SegurancaSmtp, string? UsuarioSmtp, bool TemCredencial,
    Guid? TenantGraphId, Guid? ClienteGraphId, DateTimeOffset? CredencialExpiraEm);
public sealed record ResumoIntegracoes(EmailConfigurado? Email, ConfiguracaoMicrosoft? Microsoft, bool EmailAtivo,
    bool MicrosoftAtivo, long RevisaoEmail, DateTimeOffset? UltimoTesteEm, string? UltimoDiagnostico, bool ProcessamentoAtivo = false);
public sealed record IdentidadeMicrosoft(Guid EmpresaId, Guid UsuarioId, bool Administrador, bool Ativa);
public sealed record AcessoMicrosoft(Guid TenantId, Guid ApiClienteId, Guid ClienteLoginId, string Authority, string Escopo);
public sealed record EmailSaida(Guid Id, string Destinatario, string Assunto, string Texto);
public sealed record DiagnosticoIntegracao(bool Sucesso, string Codigo, string Mensagem);
public sealed record EntregaResumo(Guid Id, Guid? UsuarioId, string Situacao, int Tentativas, DateTimeOffset? ProximaTentativaEm,
    string? Codigo, DateTimeOffset CriadaEm, string? Mensagem = null);
public static class MensagensIntegracao
{
    public static string ParaEntrega(string situacao, string? codigo) => codigo switch
    {
        "aceita_provedor" => "O provedor aceitou o envio. A entrega na caixa do destinatário depende do serviço de e-mail.",
        "credencial_invalida" or "credencial_expirada" => "Confira a credencial e sua validade, atualize a configuração e teste novamente.",
        "credencial_indisponivel" => "Confira as chaves de proteção do servidor ou cadastre novamente a credencial.",
        "permissao_insuficiente" => "Confira o consentimento do aplicativo e a permissão de envio pela caixa selecionada.",
        "limite_provedor" => "O provedor limitou os envios. A próxima tentativa respeitará a espera indicada.",
        "smtp_nao_permitido" => "Libere este servidor SMTP na configuração do servidor V8Desk.",
        "destinatario_indisponivel" => "Confira o e-mail de notificações e se a conta do destinatário está ativa.",
        "acesso_revogado" => "A notificação foi ignorada porque o destinatário perdeu acesso ao chamado.",
        "notificacoes_pausadas" => "A notificação foi ignorada porque o envio foi pausado.",
        "configuracao_alterada" => "Este teste usa uma configuração antiga. Solicite outro teste.",
        "tls_invalido" => "Confira o certificado, a porta e a segurança TLS do servidor SMTP.",
        "smtp_incompativel" => "Confira se o servidor SMTP oferece a segurança e a autenticação configuradas.",
        "caixa_indisponivel" => "Confira o e-mail remetente e a disponibilidade da caixa.",
        _ => situacao == "Pendente" ? "Aguardando processamento ou uma nova tentativa."
            : "Não foi possível concluir o envio. Confira a conexão e a configuração antes de tentar novamente."
    };
}
public interface IProtecaoCredencial
{
    string Proteger(Guid empresaId, string segredo);
    string Revelar(Guid empresaId, string protegido);
}
public interface ITransporteEmail
{
    Task<DiagnosticoIntegracao> DiagnosticarAsync(ConfiguracaoEmail config, Guid empresaId, CancellationToken ct);
    Task EnviarAsync(ConfiguracaoEmail config, Guid empresaId, EmailSaida email, CancellationToken ct);
}
public sealed class FalhaIntegracaoException(string codigo, string mensagem, bool temporaria, TimeSpan? aguardar = null) : Exception(mensagem)
{
    public string Codigo { get; } = codigo;
    public bool Temporaria { get; } = temporaria;
    public TimeSpan? Aguardar { get; } = aguardar;
}
public interface IIntegracoesRepository
{
    Task<IntegracoesEmpresa?> ObterAsync(bool atualizar, CancellationToken ct);
    void Adicionar(IntegracoesEmpresa integracoes);
    Task VincularAsync(Guid usuarioId, Guid tenantId, Guid objetoId, bool administrador, bool ativo, CancellationToken ct);
    Task ContatoAsync(Guid usuarioId, string email, CancellationToken ct);
    Task<string?> EmailAsync(Guid usuarioId, CancellationToken ct);
    Task RegistrarTesteAsync(Guid id, Guid usuarioId, long revisao, CancellationToken ct);
    Task<IReadOnlyList<EntregaResumo>> EntregasAsync(int tamanho, CancellationToken ct);
    Task RepetirEntregaAsync(Guid id, CancellationToken ct);
    void Auditar(Guid usuarioId, string acao, DateTimeOffset agora);
}
public interface IIdentidadeMicrosoftRepository
{
    Task<IdentidadeMicrosoft?> ResolverAsync(Guid tenantId, Guid apiClienteId, Guid objetoId, CancellationToken ct);
    Task<AcessoMicrosoft?> AcessoAsync(Guid empresaId, CancellationToken ct);
}
