using Microsoft.EntityFrameworkCore;
using v8desk.application.Integracoes;
using v8desk.domain.Entities;
using v8desk.domain.Exceptions;
using v8desk.infrastructure.Persistence;

namespace v8desk.infrastructure.Integracoes;

public sealed class IntegracoesRepository(V8DeskDbContext db) : IIntegracoesRepository
{
    public Task<IntegracoesEmpresa?> ObterAsync(bool atualizar, CancellationToken ct) =>
        (atualizar ? db.Set<IntegracoesEmpresa>().AsTracking() : db.Set<IntegracoesEmpresa>().AsNoTracking()).SingleOrDefaultAsync(ct);
    public void Adicionar(IntegracoesEmpresa c) => db.Add(c);
    public async Task VincularAsync(Guid usuarioId, Guid tenantId, Guid objetoId, bool administrador, bool ativo, CancellationToken ct)
    {
        var outro = await db.Set<VinculoMicrosoft>().AnyAsync(v => v.TenantId == tenantId && v.ObjetoId == objetoId && v.UsuarioId != usuarioId, ct);
        if (outro) throw new RegraNegocioException("Esta conta Microsoft já está vinculada a outro usuário.");
        var v = await db.Set<VinculoMicrosoft>().AsTracking().SingleOrDefaultAsync(v => v.UsuarioId == usuarioId && v.TenantId == tenantId, ct);
        if (v is null && !ativo) throw new RegraNegocioException("Este vínculo Microsoft não está disponível.");
        if (v is null) db.Add(new VinculoMicrosoft(db.EmpresaId, usuarioId, tenantId, objetoId, administrador, ativo));
        else
        {
            if (!ativo && v.ObjetoId != objetoId) throw new RegraNegocioException("Confira o identificador da conta Microsoft vinculada.");
            v.Atualizar(objetoId, administrador, ativo);
        }
    }
    public async Task ContatoAsync(Guid usuarioId, string email, CancellationToken ct)
    {
        var contato = await db.Set<ContatoNotificacao>().AsTracking().SingleOrDefaultAsync(c => c.UsuarioId == usuarioId, ct);
        if (contato is null) db.Add(new ContatoNotificacao(db.EmpresaId, usuarioId, email)); else contato.Atualizar(email);
    }
    public Task<string?> EmailAsync(Guid usuarioId, CancellationToken ct) =>
        db.Set<ContatoNotificacao>().Where(c => c.UsuarioId == usuarioId).Select(c => c.Email).SingleOrDefaultAsync(ct);
    public Task RegistrarTesteAsync(Guid id, Guid usuarioId, long revisao, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        db.Add(new EntregaEmail(id, db.EmpresaId, usuarioId, null, null, true, revisao, DateTimeOffset.UtcNow));
        return Task.CompletedTask;
    }
    public async Task<IReadOnlyList<EntregaResumo>> EntregasAsync(int tamanho, CancellationToken ct) =>
        await db.Set<EntregaEmail>().OrderByDescending(e => e.CriadaEm).ThenByDescending(e => e.Id).Take(tamanho)
            .Select(e => new EntregaResumo(e.Id, e.UsuarioId, e.Situacao, e.Tentativas, e.ProximaTentativaEm, e.Codigo, e.CriadaEm)).ToListAsync(ct);
    public async Task RepetirEntregaAsync(Guid id, CancellationToken ct)
    {
        var e = await db.Set<EntregaEmail>().AsTracking().SingleOrDefaultAsync(e => e.Id == id, ct)
            ?? throw new AcessoNegadoException("Esta entrega não está disponível.");
        e.Repetir(DateTimeOffset.UtcNow);
    }
    public void Auditar(Guid usuarioId, string acao, DateTimeOffset agora) => db.Add(new AuditoriaIntegracao(db.EmpresaId, usuarioId, acao, agora));
}
