using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using v8desk.application.Chamados;
using v8desk.application.Configuracao;

namespace v8desk.application;

public static class DependencyInjection
{
    public static IServiceCollection AdicionarAplicacao(this IServiceCollection services)
    {
        services.TryAddSingleton<TimeProvider>(TimeProvider.System);
        services.AddScoped<ChamadoAplicacao>();
        services.AddScoped<ConfiguracaoSetorAplicacao>();
        services.AddScoped<ConfiguracaoEmpresaAplicacao>();
        services.AddScoped<EncerramentoAutomaticoAplicacao>();
        services.AddScoped<v8desk.application.Usuarios.UsuarioAplicacao>();
        return services;
    }
}
