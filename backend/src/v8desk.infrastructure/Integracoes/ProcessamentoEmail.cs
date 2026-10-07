using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using v8desk.application.Abstractions;
using v8desk.application.Integracoes;
using v8desk.application.Seguranca;
using v8desk.domain.Entities;
using v8desk.domain.Enums;
using v8desk.infrastructure.Persistence;

namespace v8desk.infrastructure.Integracoes;

// Converte eventos em entregas duráveis na transação da outbox, sem chamar o provedor.
public sealed class PublicadorEmailOutbox(V8DeskDbContext db) : IPublicadorOutbox
{
    public async Task PublicarAsync(EntregaOutbox mensagem, CancellationToken ct)
    {
        if (mensagem.EmpresaId != db.EmpresaId) throw new InvalidOperationException("Escopo de empresa inválido.");
        var config = await db.Set<IntegracoesEmpresa>().AsNoTracking().SingleOrDefaultAsync(ct);
        if (config?.EmailAtivo != true) return;
        var evento = JsonSerializer.Deserialize<EventoChamado>(mensagem.Payload) ?? throw new InvalidOperationException("Evento inválido.");
        if (evento.Tipo is TipoEventoChamado.NotaInternaAdicionada or TipoEventoChamado.MensagemCorrigida or TipoEventoChamado.AnexoAdicionado) return;
        var chamado = await db.Chamados.AsNoTracking().Where(c => c.Id == evento.ChamadoId)
            .Select(c => new { c.Id, c.SolicitanteId, ResponsavelId = EF.Property<Guid?>(c, "ResponsavelConsultaId") }).SingleOrDefaultAsync(ct);
        if (chamado is null) return;
        var usuarios = new Guid?[] { chamado.SolicitanteId, chamado.ResponsavelId }.Where(u => u.HasValue)
            .Select(u => u!.Value).Distinct().Where(u => u != evento.AutorId || evento.Tipo == TipoEventoChamado.Aberto);
        foreach (var usuario in usuarios)
        {
            if (await db.Set<EntregaEmail>().AnyAsync(e => e.EventoId == evento.Id && e.UsuarioId == usuario, ct)) continue;
            db.Add(new EntregaEmail(Guid.CreateVersion7(), db.EmpresaId, usuario, evento.Id, chamado.Id, false, config.RevisaoEmail, DateTimeOffset.UtcNow));
        }
    }
}
public sealed class ProcessadorEmail(V8DeskDbContext db, IIntegracoesRepository repo, IAcessoRepository acessos,
    ITransporteEmail transporte, TimeProvider relogio)
{
    // O host usa um contexto novo sem retry de execução automática envolvendo o envio externo.
    public async Task<int> ProcessarAsync(int tamanho = 20, CancellationToken ct = default)
    {
        if (tamanho is < 1 or > 100 || db.ChangeTracker.Entries().Any() || db.Database.CurrentTransaction is not null)
            throw new InvalidOperationException("Use um contexto novo e lotes de 1 a 100 entregas.");
        var aceitas = 0;
        for (var i = 0; i < tamanho; i++)
        {
            db.ChangeTracker.Clear();
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var agora = relogio.GetUtcNow();
            var entrega = (await db.Set<EntregaEmail>().FromSqlInterpolated($"""
                SELECT entregas_email.*, xmin FROM entregas_email
                WHERE empresa_id = {db.EmpresaId} AND situacao = 'Pendente' AND proxima_tentativa_em <= {agora}
                ORDER BY proxima_tentativa_em, criada_em, id LIMIT 1 FOR UPDATE SKIP LOCKED
                """).AsTracking().ToListAsync(ct)).SingleOrDefault();
            if (entrega is null) { await tx.CommitAsync(ct); break; }
            var config = await repo.ObterAsync(true, ct);
            if (await TentarEntregaAsync(entrega, config, ct)) aceitas++;
            // Erros internos e cancelamento do host revertem a transação; não se registra conteúdo/credencial.
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        return aceitas;
    }
    internal async Task<bool> TentarEntregaAsync(EntregaEmail entrega, IntegracoesEmpresa? config, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (entrega.EmpresaId != db.EmpresaId || config is not null && config.EmpresaId != db.EmpresaId)
            throw new v8desk.domain.Exceptions.AcessoNegadoException("Esta entrega não está disponível nesta empresa.");
        var agora = relogio.GetUtcNow();
        var aceita = false;
        using var prazo = CancellationTokenSource.CreateLinkedTokenSource(ct);
        prazo.CancelAfter(TimeSpan.FromSeconds(30));
        try
        {
            if (config?.Email is null) throw new FalhaIntegracaoException("nao_configurado", "Configure o e-mail.", false);
            if (!entrega.Teste && !config.EmailAtivo) { entrega.Ignorar("notificacoes_pausadas"); }
            else if (entrega.Teste && entrega.RevisaoConfiguracao != config.RevisaoEmail) { entrega.Ignorar("configuracao_alterada"); }
            else
            {
                var email = await repo.EmailAsync(entrega.UsuarioId, prazo.Token);
                var usuario = await acessos.ObterUsuarioAsync(entrega.UsuarioId, prazo.Token);
                if (usuario?.Ativo != true || usuario.EmpresaId != db.EmpresaId || email is null)
                    throw new FalhaIntegracaoException("destinatario_indisponivel", "Confira o e-mail e a atividade do destinatário.", false);
                long? numero = null;
                if (!entrega.Teste && entrega.ChamadoId is { } id)
                {
                    var referencia = await acessos.ObterReferenciaAsync(id, prazo.Token);
                    var dados = referencia is null ? null : await acessos.ObterDadosAsync(entrega.UsuarioId, [referencia.FilaAtualId], prazo.Token);
                    if (referencia is null || dados is null || dados.Usuario.Id != entrega.UsuarioId ||
                        !new AutorizacaoChamado(db.EmpresaId, dados).PodeVisualizar(referencia))
                    { entrega.Ignorar("acesso_revogado"); }
                    else numero = await db.Chamados.Where(c => c.Id == id).Select(c => c.Numero).SingleAsync(prazo.Token);
                }
                if (entrega.Situacao == "Pendente")
                {
                    var assunto = entrega.Teste ? "Teste de e-mail do V8Desk" : "Atualização no chamado #" + numero;
                    var texto = entrega.Teste ? "A conexão de e-mail do V8Desk está funcionando. Este envio foi solicitado por um administrador."
                        : "Há uma atualização no chamado #" + numero + ". Entre no V8Desk para acompanhar o atendimento.";
                    await transporte.EnviarAsync(config.Email, db.EmpresaId, new(entrega.Id, email, assunto, texto), prazo.Token);
                    entrega.Aceitar(); aceita = true;
                    if (entrega.Teste) config.RegistrarTeste(entrega.RevisaoConfiguracao, true, "aceita_provedor", agora);
                }
            }
        }
        catch (FalhaIntegracaoException erro)
        {
            entrega.Falhar(erro.Codigo, erro.Temporaria, erro.Aguardar, agora);
            if (entrega.Teste) config?.RegistrarTeste(entrega.RevisaoConfiguracao, false, erro.Codigo, agora);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        { entrega.Falhar("provedor_timeout", true, null, agora); }
        return aceita;
    }
}
