using System.Net;
using Microsoft.AspNetCore.HttpOverrides;

namespace v8desk.api.Security;

public static class ProxyConfiavel
{
    public static bool AdicionarProxyConfiavel(this IServiceCollection services, IConfiguration config)
    {
        var valores = config.GetSection("ReverseProxy:KnownProxies").Get<string[]>() ?? [];
        if (valores.Length == 0) return false;
        var enderecos = valores.Select(valor => IPAddress.TryParse(valor, out var ip) ? ip
            : throw new InvalidOperationException("Informe endereços IP válidos em ReverseProxy:KnownProxies.")).ToArray();
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.ForwardLimit = 1;
            options.KnownProxies.Clear();
            options.KnownIPNetworks.Clear();
            foreach (var ip in enderecos) options.KnownProxies.Add(ip);
        });
        return true;
    }
}
