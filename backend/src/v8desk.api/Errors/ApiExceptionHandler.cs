using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using v8desk.domain.Exceptions;

namespace v8desk.api.Errors;

public sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception,
        CancellationToken cancellationToken)
    {
        if (context.Response.HasStarted) return false;
        var (status, titulo, detalhe, codigo) = exception switch
        {
            ValidacaoDominioException erro => (400, "Confira os dados informados", erro.Message, erro.Codigo),
            AcessoNegadoException erro => (403, "Ação não permitida", erro.Message, "acesso_negado"),
            RegraNegocioException erro => (409, "Não foi possível concluir esta ação", erro.Message, "regra_negocio"),
            _ => (500, "Não foi possível concluir a solicitação",
                "Ocorreu um erro inesperado. Tente novamente. Se continuar, informe o código de atendimento ao suporte.", "erro_inesperado")
        };
        var traceId = Activity.Current?.Id ?? context.TraceIdentifier;
        if (status == 500) logger.LogError(exception, "Falha inesperada. Atendimento: {TraceId}", traceId);

        var problema = new ProblemDetails { Status = status, Title = titulo, Detail = detalhe };
        problema.Extensions["code"] = codigo;
        problema.Extensions["traceId"] = traceId;
        if (exception is ValidacaoDominioException validacao)
            problema.Extensions["errors"] = new Dictionary<string, string[]> { [validacao.Campo] = [validacao.Message] };
        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(problema, options: (System.Text.Json.JsonSerializerOptions?)null,
            contentType: "application/problem+json", cancellationToken: cancellationToken);
        return true;
    }
}
