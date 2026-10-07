using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using v8desk.application.Abstractions;
using v8desk.infrastructure.Persistence;
using v8desk.infrastructure.Repositories;

namespace v8desk.infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AdicionarIntegracoes(this IServiceCollection services, string conexao, IEnumerable<string> hostsSmtp)
    {
        services.AddSingleton(_ => Npgsql.NpgsqlDataSource.Create(conexao));
        services.AddScoped<v8desk.application.Integracoes.IIntegracoesRepository, Integracoes.IntegracoesRepository>();
        services.AddSingleton<v8desk.application.Integracoes.IIdentidadeMicrosoftRepository, Integracoes.IdentidadeMicrosoftRepository>();
        services.AddSingleton(new Integracoes.OpcoesEmail(new HashSet<string>(hostsSmtp, StringComparer.OrdinalIgnoreCase)));
        services.AddSingleton<Integracoes.ISessaoSmtpFactory, Integracoes.SessaoSmtpFactory>();
        services.AddScoped<v8desk.application.Integracoes.ITransporteEmail, Integracoes.TransporteEmail>();
        services.AddScoped<v8desk.application.Integracoes.IntegracoesAplicacao>();
        services.AddHttpClient("integracoes", client => client.Timeout = TimeSpan.FromSeconds(15))
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AllowAutoRedirect = false, ConnectTimeout = TimeSpan.FromSeconds(10) })
            .RedactLoggedHeaders(_ => true);
        return services;
    }
    public static IServiceCollection AdicionarInfraestrutura(this IServiceCollection services, string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("Configure ConnectionStrings:V8Desk por variável de ambiente ou user-secrets.");
        services.AddDbContext<V8DeskDbContext>(options => options
            .UseNpgsql(connectionString, pg => pg.EnableRetryOnFailure(3, TimeSpan.FromSeconds(5), null)
                .CommandTimeout(30).MigrationsHistoryTable("__ef_migrations_history"))
            .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking));
        services.AddScoped<IChamadoRepository, ChamadoRepository>();
        services.AddScoped<IConfiguracaoRepository, ConfiguracaoRepository>();
        services.AddScoped<IAcessoRepository, AcessoRepository>();
        services.AddScoped<IUnidadeTrabalho>(sp => sp.GetRequiredService<V8DeskDbContext>());
        services.AddScoped<ChamadoConsultas>();
        services.AddScoped<v8desk.application.Chamados.IProntuarioConsultas, ProntuarioConsultas>();
        services.AddScoped<v8desk.application.Chamados.IChamadoConsultas>(sp => sp.GetRequiredService<ChamadoConsultas>());
        services.AddScoped<IExecutorComandoIdempotente, ExecutorComandoIdempotente>();
        return services;
    }

    // O host opta pelo processamento somente depois de fornecer um transporte real.
    public static IServiceCollection AdicionarProcessamentoOutbox<TPublicador>(this IServiceCollection services)
        where TPublicador : class, IPublicadorOutbox
    {
        services.AddScoped<IPublicadorOutbox, TPublicador>();
        services.AddScoped<OutboxProcessador>();
        return services;
    }
}
