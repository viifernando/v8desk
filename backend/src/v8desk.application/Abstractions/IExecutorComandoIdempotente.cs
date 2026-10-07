namespace v8desk.application.Abstractions;

public interface IExecutorComandoIdempotente
{
    // O conteúdo canônico inclui nome/versão do comando e todos os seus parâmetros.
    // A autorização deve ocorrer antes da chamada, inclusive em replays.
    Task<string> ExecutarAsync(string chave, string conteudoCanonico,
        Func<CancellationToken, Task<string>> comando, CancellationToken cancellationToken = default);
}
