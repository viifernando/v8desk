using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using v8desk.application.Abstractions;
using v8desk.infrastructure.Persistence;
using v8desk.infrastructure.Repositories;

namespace v8desk.infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AdicionarInfraestrutura(this IServiceCollection services, string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("Configure ConnectionStrings:V8Desk por variável de ambiente ou user-secrets.");
        services.AddDbContext<V8DeskDbContext>(options => options
            .UseNpgsql(connectionString, pg => pg.EnableRetryOnFailure(3, TimeSpan.FromSeconds(5), null)
                .CommandTimeout(30).MigrationsHistoryTable("__ef_migrations_history"))
            .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking));
        services.AddScoped<IChamadoRepository, ChamadoRepository>();
        services.AddScoped<IUnidadeTrabalho>(sp => sp.GetRequiredService<V8DeskDbContext>());
        services.AddScoped<ChamadoConsultas>();
        return services;
    }
}
