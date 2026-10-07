using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace v8desk.api.Errors;

public static class ProblemasApi
{
    public static ProblemDetails Criar(HttpContext context, int status, string titulo, string detalhe, string codigo)
    {
        var problema = new ProblemDetails
        {
            Status = status, Title = titulo, Detail = detalhe, Instance = context.Request.Path
        };
        problema.Extensions["code"] = codigo;
        problema.Extensions["traceId"] = Activity.Current?.Id ?? context.TraceIdentifier;
        return problema;
    }

    public static Task EscreverAsync(HttpContext context, int status, string titulo, string detalhe, string codigo,
        CancellationToken ct = default)
    {
        context.Response.StatusCode = status;
        return context.Response.WriteAsJsonAsync(Criar(context, status, titulo, detalhe, codigo),
            options: (System.Text.Json.JsonSerializerOptions?)null, contentType: "application/problem+json", cancellationToken: ct);
    }

    public static (string Titulo, string Detalhe, string Codigo) ParaStatus(int status) => status switch
    {
        401 => ("Entre para continuar", "Sua sessão não está válida. Entre novamente para continuar.", "autenticacao_necessaria"),
        403 => ("Ação não permitida", "Sua conta não tem permissão para esta ação.", "acesso_negado"),
        404 => ("Endereço não encontrado", "Confira o endereço solicitado.", "rota_nao_encontrada"),
        405 => ("Operação não disponível", "Este endereço não aceita o tipo de operação enviado.", "metodo_nao_permitido"),
        413 => ("Solicitação muito grande", "Reduza o tamanho dos dados enviados e tente novamente.", "requisicao_grande"),
        415 => ("Formato não aceito", "Envie os dados em JSON, com Content-Type application/json.", "formato_nao_aceito"),
        429 => ("Aguarde um momento", "Você fez muitas solicitações. Aguarde antes de tentar novamente.", "limite_requisicoes"),
        503 => ("Serviço temporariamente indisponível", "Não foi possível acessar o serviço agora. Tente novamente em alguns instantes.", "servico_indisponivel"),
        _ => ("Confira a solicitação", "Não foi possível processar a solicitação enviada.", "requisicao_invalida")
    };
}
