using v8desk.application.Abstractions;
using v8desk.application.Common;
using v8desk.domain.Entities;
using v8desk.domain.Exceptions;
using v8desk.domain.ValueObjects;

namespace v8desk.application.Integracoes;

public sealed record AtivarEmail(bool Ativo);
public sealed record AtivarMicrosoft(bool Ativo);
public sealed record DesvincularMicrosoft(Guid UsuarioId, Guid ObjetoId);
public sealed record TestarEmail;
public sealed record RepetirEntrega(Guid Id);

public sealed class IntegracoesAplicacao(IIntegracoesRepository repository, IAcessoRepository acessos,
    IAutorizacaoEmpresa autorizacao, IProtecaoCredencial protecao, ITransporteEmail transporte,
    IExecutorComandoIdempotente executor, IUnidadeTrabalho unidade, IEmpresaAtual empresa, IUsuarioAtual usuario, TimeProvider relogio)
{
    public async Task<ResumoIntegracoes> ObterAsync(CancellationToken ct = default)
    {
        await ExigirAsync(ct);
        var c = await repository.ObterAsync(false, ct);
        var e = c?.Email;
        return new(e is null ? null : new(e.Provedor, e.Remetente, e.NomeRemetente, e.HostSmtp, e.PortaSmtp, e.SegurancaSmtp,
            e.UsuarioSmtp, e.CredencialProtegida is not null, e.TenantGraphId, e.ClienteGraphId, e.CredencialExpiraEm),
            c?.Microsoft, c?.EmailAtivo ?? false, c?.MicrosoftAtivo ?? false, c?.RevisaoEmail ?? 0, c?.UltimoTesteEm, c?.UltimoDiagnostico);
    }
    public async Task<ResultadoIntegracao> ExecutarAsync<T>(T comando, string chave, CancellationToken ct = default) where T : notnull
    {
        await ExigirAsync(ct);
        return await ComandoIdempotente.ExecutarAsync(executor, chave, comando, async token =>
        {
            await ExigirAsync(token);
            var c = await repository.ObterAsync(true, token);
            if (c is null) { c = new(empresa.EmpresaId); repository.Adicionar(c); }
            if (c.EmpresaId != empresa.EmpresaId) throw new AcessoNegadoException("Esta integração não está disponível.");
            var id = c.Id;
            var mensagem = "Configuração salva.";
            switch (comando)
            {
                case ConfigurarEmail e:
                    string? segredo = null;
                    if (e.Segredo is not null)
                    {
                        if (string.IsNullOrWhiteSpace(e.Segredo) || e.Segredo.Length > 4096)
                            throw new ValidacaoDominioException("credencial_invalida", "segredo", "Informe uma credencial válida, com até 4.096 caracteres.");
                        segredo = protecao.Proteger(empresa.EmpresaId, e.Segredo);
                    }
                    else if (c.Email is { } anterior && anterior.Provedor == e.Provedor &&
                        (e.Provedor == ProvedorEmail.Smtp ? anterior.HostSmtp == e.HostSmtp && anterior.UsuarioSmtp == e.UsuarioSmtp
                            : anterior.TenantGraphId == e.TenantGraphId && anterior.ClienteGraphId == e.ClienteGraphId))
                        segredo = anterior.CredencialProtegida;
                    c.ConfigurarEmail(new()
                    {
                        Provedor = e.Provedor, Remetente = e.Remetente, NomeRemetente = e.NomeRemetente,
                        HostSmtp = e.HostSmtp, PortaSmtp = e.PortaSmtp, SegurancaSmtp = e.SegurancaSmtp, UsuarioSmtp = e.UsuarioSmtp,
                        CredencialProtegida = segredo, TenantGraphId = e.TenantGraphId, ClienteGraphId = e.ClienteGraphId,
                        CredencialExpiraEm = e.CredencialExpiraEm
                    });
                    mensagem = "Configuração salva. Envie um e-mail de teste antes de ativar."; break;
                case ConfigurarMicrosoft m:
                    c.ConfigurarMicrosoft(new(m.TenantId, m.ApiClienteId, m.ClienteLoginId));
                    mensagem = "Conexão configurada. Vincule os usuários e ative o acesso Microsoft."; break;
                case AtivarEmail e: c.AtivarEmail(e.Ativo, relogio.GetUtcNow()); mensagem = e.Ativo ? "Notificações ativadas." : "Notificações pausadas."; break;
                case AtivarMicrosoft m: c.AtivarMicrosoft(m.Ativo); mensagem = m.Ativo ? "Acesso Microsoft ativado." : "Acesso Microsoft desativado."; break;
                case VincularMicrosoft m:
                    await UsuarioAsync(m.UsuarioId, true, token);
                    if (c.Microsoft is null) throw new RegraNegocioException("Configure o Microsoft Entra antes de vincular usuários.");
                    if (m.ObjetoId == Guid.Empty) throw new ValidacaoDominioException("objeto_invalido", "objetoId", "Informe o identificador do usuário no Microsoft Entra.");
                    await repository.VincularAsync(m.UsuarioId, c.Microsoft.TenantId, m.ObjetoId, m.Administrador, true, token);
                    mensagem = "Conta Microsoft vinculada ao usuário."; break;
                case DesvincularMicrosoft m:
                    await UsuarioAsync(m.UsuarioId, false, token);
                    if (c.Microsoft is null) throw new RegraNegocioException("A conexão Microsoft não está configurada.");
                    await repository.VincularAsync(m.UsuarioId, c.Microsoft.TenantId, m.ObjetoId, false, false, token);
                    mensagem = "Vínculo Microsoft desativado."; break;
                case ConfigurarContato contato:
                    await UsuarioAsync(contato.UsuarioId, true, token);
                    await repository.ContatoAsync(contato.UsuarioId, ConfiguracaoEmail.EmailValido(contato.Email), token);
                    mensagem = "E-mail de notificações atualizado."; break;
                case TestarEmail:
                    if (c.Email is null) throw new RegraNegocioException("Configure o provedor de e-mail antes do teste.");
                    if (await repository.EmailAsync(usuario.UsuarioId, token) is null)
                        throw new RegraNegocioException("Cadastre seu e-mail de notificações antes de solicitar o teste.");
                    id = Guid.CreateVersion7();
                    await repository.RegistrarTesteAsync(id, usuario.UsuarioId, c.RevisaoEmail, token);
                    mensagem = "Teste agendado para seu e-mail. Consulte o resultado em entregas."; break;
                case RepetirEntrega e:
                    await repository.RepetirEntregaAsync(e.Id, token);
                    id = e.Id; mensagem = "Entrega agendada para uma nova tentativa."; break;
                default: throw new ValidacaoDominioException("comando_invalido", "comando", "Esta configuração não é reconhecida.");
            }
            repository.Auditar(usuario.UsuarioId, typeof(T).Name, relogio.GetUtcNow());
            await unidade.SalvarAsync(token);
            return new ResultadoIntegracao(id, mensagem);
        }, ct);
    }
    public async Task<DiagnosticoIntegracao> DiagnosticarAsync(CancellationToken ct = default)
    {
        await ExigirAsync(ct);
        var c = await repository.ObterAsync(false, ct);
        if (c?.Email is null) return new(false, "nao_configurado", "Configure o provedor de e-mail para testar a conexão.");
        try { return await transporte.DiagnosticarAsync(c.Email, empresa.EmpresaId, ct); }
        catch (FalhaIntegracaoException e) { return new(false, e.Codigo, e.Message); }
    }
    public async Task<IReadOnlyList<EntregaResumo>> EntregasAsync(int tamanho, CancellationToken ct = default)
    {
        await ExigirAsync(ct);
        if (tamanho is < 1 or > 100) throw new ValidacaoDominioException("pagina_invalida", "tamanho", "Use páginas de 1 a 100 entregas.");
        var entregas = await repository.EntregasAsync(tamanho, ct);
        return entregas.Select(e => e with { Mensagem = MensagensIntegracao.ParaEntrega(e.Situacao, e.Codigo) }).ToArray();
    }
    private async Task ExigirAsync(CancellationToken ct)
    {
        await autorizacao.ExigirAdministracaoAsync(empresa.EmpresaId, usuario.UsuarioId, ct);
        await UsuarioAsync(usuario.UsuarioId, true, ct);
    }
    private async Task UsuarioAsync(Guid id, bool exigirAtivo, CancellationToken ct)
    {
        var u = await acessos.ObterUsuarioAsync(id, ct);
        if (u is null || u.Id != id || u.EmpresaId != empresa.EmpresaId || exigirAtivo && !u.Ativo)
            throw new AcessoNegadoException("O usuário não está disponível nesta empresa.");
    }
}
