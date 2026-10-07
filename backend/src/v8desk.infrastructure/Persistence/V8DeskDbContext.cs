using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using v8desk.application.Abstractions;
using v8desk.domain.Entities;
using v8desk.domain.Exceptions;

namespace v8desk.infrastructure.Persistence;

public sealed class V8DeskDbContext : DbContext, IUnidadeTrabalho
{
    public Guid EmpresaId { get; }
    public DbSet<Empresa> Empresas => Set<Empresa>();
    public DbSet<Setor> Setores => Set<Setor>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<VinculoSetor> Vinculos => Set<VinculoSetor>();
    public DbSet<Fila> Filas => Set<Fila>();
    public DbSet<Categoria> Categorias => Set<Categoria>();
    public DbSet<Chamado> Chamados => Set<Chamado>();
    public DbSet<EventoChamado> Eventos => Set<EventoChamado>();
    public DbSet<Mensagem> Mensagens => Set<Mensagem>();
    public DbSet<PeriodoEtapa> Periodos => Set<PeriodoEtapa>();
    public DbSet<CicloSla> Slas => Set<CicloSla>();

    public V8DeskDbContext(DbContextOptions<V8DeskDbContext> options, IEmpresaAtual empresa) : base(options)
    {
        EmpresaId = empresa.EmpresaId;
        if (EmpresaId == Guid.Empty) throw new AcessoNegadoException("Identifique sua empresa para continuar.");
        ChangeTracker.Tracked += (_, evento) =>
        {
            if (evento.Entry.State == EntityState.Added &&
                evento.Entry.Metadata.FindProperty("EmpresaId") is { } propriedade && propriedade.IsShadowProperty())
            {
                var campo = evento.Entry.Property("EmpresaId");
                if (campo.CurrentValue is Guid id && id == Guid.Empty) campo.CurrentValue = EmpresaId;
            }
        };
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        Mapeamentos.Configurar(modelBuilder);
        foreach (var entidade in modelBuilder.Model.GetEntityTypes())
        {
            var parametro = Expression.Parameter(entidade.ClrType, "registro");
            var propriedade = entidade.ClrType == typeof(Empresa) ? "Id" : "EmpresaId";
            var identificador = Expression.Call(typeof(EF), nameof(EF.Property), [typeof(Guid)],
                parametro, Expression.Constant(propriedade));
            var empresaAtual = Expression.Property(Expression.Constant(this), nameof(EmpresaId));
            entidade.SetQueryFilter(Expression.Lambda(Expression.Equal(identificador, empresaAtual), parametro));
        }
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        PrepararGravacao();
        try { return base.SaveChanges(acceptAllChangesOnSuccess); }
        catch (DbUpdateConcurrencyException) { throw ConflitoConcorrencia(); }
        catch (DbUpdateException erro) when (erro.InnerException is Npgsql.PostgresException pg && ErrosPersistencia.Traduzir(pg) is not null)
        { throw ErrosPersistencia.Traduzir((Npgsql.PostgresException)erro.InnerException!)!; }
    }

    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        PrepararGravacao();
        try { return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken); }
        catch (DbUpdateConcurrencyException) { throw ConflitoConcorrencia(); }
        catch (DbUpdateException erro) when (erro.InnerException is Npgsql.PostgresException pg && ErrosPersistencia.Traduzir(pg) is not null)
        { throw ErrosPersistencia.Traduzir((Npgsql.PostgresException)erro.InnerException!)!; }
    }

    public Task<int> SalvarAsync(CancellationToken cancellationToken = default) => SaveChangesAsync(cancellationToken);

    private void PrepararGravacao()
    {
        ChangeTracker.DetectChanges();
        foreach (var entrada in ChangeTracker.Entries().Where(e => e.State is EntityState.Added or EntityState.Modified))
        {
            if (entrada.Entity is Fila fila) entrada.Property("NomeComparacao").CurrentValue = fila.Nome.ToUpperInvariant();
            if (entrada.Entity is Categoria categoria) entrada.Property("NomeComparacao").CurrentValue = categoria.Nome.ToUpperInvariant();
        }
        foreach (var evento in ChangeTracker.Entries<EventoChamado>().Where(e => e.State == EntityState.Added).ToArray())
        {
            if (!ChangeTracker.Entries<OutboxMensagem>().Any(e => e.Entity.Id == evento.Entity.Id))
                Add(new OutboxMensagem(evento.Entity.Id, EmpresaId, evento.Entity.Tipo.ToString(),
                    JsonPersistencia.Escrever(evento.Entity), evento.Entity.OcorridoEm));
        }
        foreach (var entrada in ChangeTracker.Entries().Where(e => e.State != EntityState.Unchanged && e.State != EntityState.Detached))
        {
            if (entrada.Entity is Empresa empresa)
            {
                if (empresa.Id != EmpresaId) throw new AcessoNegadoException("Não é permitido gravar dados de outra empresa.");
            }
            else
            {
                var campo = entrada.Property("EmpresaId");
                if (entrada.State == EntityState.Added && entrada.Metadata.FindProperty("EmpresaId")!.IsShadowProperty())
                {
                    if (campo.CurrentValue is Guid informado && informado != Guid.Empty && informado != EmpresaId)
                        throw new AcessoNegadoException("Não é permitido gravar dados de outra empresa.");
                    campo.CurrentValue = EmpresaId;
                }
                if ((Guid)campo.CurrentValue! != EmpresaId ||
                    (entrada.State != EntityState.Added && (Guid)campo.OriginalValue! != EmpresaId))
                    throw new AcessoNegadoException("Não é permitido gravar dados de outra empresa.");
            }
            if (entrada.State == EntityState.Deleted && entrada.Entity is not MembroFila)
                throw new RegraNegocioException("Este registro faz parte do histórico. Desative-o em vez de excluí-lo.");
            if (entrada.State == EntityState.Modified && entrada.Entity is EventoChamado or v8desk.infrastructure.Integracoes.AuditoriaIntegracao)
                throw new RegraNegocioException("Os registros de histórico e auditoria não podem ser alterados.");
        }
    }

    private static RegraNegocioException ConflitoConcorrencia() => new(
        "Este registro foi atualizado por outra pessoa. Atualize a tela e tente novamente.");
}
