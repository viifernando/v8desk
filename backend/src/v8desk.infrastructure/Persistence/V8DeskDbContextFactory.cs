using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using v8desk.application.Abstractions;

namespace v8desk.infrastructure.Persistence;

public sealed class V8DeskDbContextFactory : IDesignTimeDbContextFactory<V8DeskDbContext>
{
    public V8DeskDbContext CreateDbContext(string[] args)
    {
        // Geração de migrations não abre conexão. Para aplicar, forneça a conexão real.
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__V8Desk")
            ?? "Host=localhost;Database=v8desk;Username=v8desk";
        return new(new DbContextOptionsBuilder<V8DeskDbContext>()
            .UseNpgsql(connection, pg => pg.MigrationsHistoryTable("__ef_migrations_history")).Options,
            new EmpresaDesignTime());
    }

    private sealed class EmpresaDesignTime : IEmpresaAtual
    {
        public Guid EmpresaId => Guid.Parse("11111111-1111-1111-1111-111111111111");
    }
}
