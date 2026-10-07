using Microsoft.AspNetCore.DataProtection;
using System.Security.Cryptography.X509Certificates;
using v8desk.application.Integracoes;

namespace v8desk.api.Integracoes;

public static class ComposicaoIntegracoes
{
    public static void AdicionarIntegracoesApi(this IServiceCollection services, IConfiguration config, bool desenvolvimento)
    {
        var dp = services.AddDataProtection().SetApplicationName("V8Desk");
        var pasta = config["Integrations:DataProtectionKeysPath"];
        if (!desenvolvimento && string.IsNullOrWhiteSpace(pasta))
            throw new InvalidOperationException("Configure Integrations:DataProtectionKeysPath em um volume persistente.");
        if (!string.IsNullOrWhiteSpace(pasta))
            dp.PersistKeysToFileSystem(new DirectoryInfo(Path.GetFullPath(pasta)));
        var arquivo = config["Integrations:DataProtectionCertificatePath"];
        if (!string.IsNullOrWhiteSpace(arquivo))
            dp.ProtectKeysWithCertificate(X509CertificateLoader.LoadPkcs12FromFile(arquivo,
                config["Integrations:DataProtectionCertificatePassword"], X509KeyStorageFlags.EphemeralKeySet));
        else if (OperatingSystem.IsWindows()) dp.ProtectKeysWithDpapi();
        else if (!desenvolvimento) throw new InvalidOperationException("Configure um certificado para proteger as chaves de integração neste servidor.");
        services.AddSingleton<IProtecaoCredencial, ProtecaoCredencial>();
        v8desk.infrastructure.DependencyInjection.AdicionarIntegracoes(services, config.GetConnectionString("V8Desk")!,
            config.GetSection("Integrations:SmtpHostsPermitidos").Get<string[]>() ?? []);
        if (config.GetValue("Integrations:WorkerEnabled", false)) services.AddHostedService<EmailWorker>();
    }
}
