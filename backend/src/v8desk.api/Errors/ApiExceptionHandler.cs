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
        if (exception is OperationCanceledException && context.RequestAborted.IsCancellationRequested)
        {
            if (context.Features.Get<Microsoft.AspNetCore.Http.Timeouts.IHttpRequestTimeoutFeature>()
                ?.RequestTimeoutToken.IsCancellationRequested == true)
            {
                context.Response.Headers.RetryAfter = "5";
                await ProblemasApi.EscreverAsync(context, 503, "Serviço temporariamente indisponível",
                    "O serviço demorou para responder. Tente novamente usando a mesma chave de operação.",
                    "prazo_requisicao_excedido", cancellationToken);
                return true;
            }
            context.Response.StatusCode = 499;
            return true; // Cliente desconectado: não tentar escrever uma resposta ou registrar como falha interna.
        }
        var (status, titulo, detalhe, codigo) = exception switch
        {
            BadHttpRequestException erro => (erro.StatusCode, ProblemasApi.ParaStatus(erro.StatusCode).Titulo,
                ProblemasApi.ParaStatus(erro.StatusCode).Detalhe, ProblemasApi.ParaStatus(erro.StatusCode).Codigo),
            TimeoutException => (503, "Serviço temporariamente indisponível",
                "O serviço demorou para responder. Tente novamente em alguns instantes, usando a mesma chave de operação.", "servico_indisponivel"),
            Microsoft.EntityFrameworkCore.Storage.RetryLimitExceededException => (503, "Serviço temporariamente indisponível",
                "Não foi possível acessar os dados agora. Tente novamente em alguns instantes, usando a mesma chave de operação.", "servico_indisponivel"),
            Npgsql.NpgsqlException erro when erro.IsTransient => (503, "Serviço temporariamente indisponível",
                "Não foi possível acessar os dados agora. Tente novamente em alguns instantes, usando a mesma chave de operação.", "servico_indisponivel"),
            ValidacaoDominioException erro => (400, "Confira os dados informados", erro.Message, erro.Codigo),
            AcessoNegadoException erro => (403, "Ação não permitida", erro.Message, "acesso_negado"),
            RegraNegocioException erro => (409, "Não foi possível concluir esta ação", erro.Message, "regra_negocio"),
            _ => (500, "Não foi possível concluir a solicitação",
                "Ocorreu um erro inesperado. Tente novamente. Se continuar, informe o código de atendimento ao suporte.", "erro_inesperado")
        };
        var traceId = Activity.Current?.Id ?? context.TraceIdentifier;
        if (status == 500) logger.LogError(exception, "Falha inesperada. Atendimento: {TraceId}", traceId);

        if (status == 503)
        {
            logger.LogWarning(exception, "Dependência indisponível. Atendimento: {TraceId}", traceId);
            context.Response.Headers.RetryAfter = "5";
        }
        var problema = ProblemasApi.Criar(context, status, titulo, detalhe, codigo);
        if (exception is ValidacaoDominioException validacao)
            problema.Extensions["errors"] = new Dictionary<string, string[]> { [validacao.Campo] = [validacao.Message] };
        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(problema, options: (System.Text.Json.JsonSerializerOptions?)null,
            contentType: "application/problem+json", cancellationToken: cancellationToken);
        return true;
    }
}
