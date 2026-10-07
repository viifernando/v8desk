using System.Text.Json;
using v8desk.application.Abstractions;

namespace v8desk.application.Common;

internal static class ComandoIdempotente
{
    public static async Task<T> ExecutarAsync<T>(IExecutorComandoIdempotente executor, string chave, object comando,
        Func<CancellationToken, Task<T>> executar, CancellationToken cancellationToken)
    {
        var conteudo = comando.GetType().FullName + ":v1:" + JsonSerializer.Serialize(comando, comando.GetType());
        var json = await executor.ExecutarAsync(chave, conteudo,
            async ct => JsonSerializer.Serialize(await executar(ct)), cancellationToken);
        return JsonSerializer.Deserialize<T>(json) ?? throw new InvalidOperationException("O resultado persistido da operação está inválido.");
    }
}
