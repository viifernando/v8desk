using Microsoft.AspNetCore.Mvc;

namespace v8desk.api.Errors;

public static class ValidacaoHttp
{
    public static IActionResult Responder(ActionContext action)
    {
        var campos = action.ModelState.Where(x => x.Value?.Errors.Count > 0).ToDictionary(
            x => string.IsNullOrEmpty(x.Key) || x.Key == "$" ? "corpo" : x.Key.TrimStart('$', '.'),
            x => x.Value!.Errors.Select(erro =>
            {
                // Somente mensagens em português definidas pelas anotações são devolvidas.
                // Mensagens do parser podem conter tipos, caminhos e valores internos.
                if (erro.Exception is null && new[] { "Informe", "Escolha", "Use", "Escreva", "Descreva", "A data" }
                    .Any(prefixo => erro.ErrorMessage.StartsWith(prefixo, StringComparison.Ordinal)))
                    return erro.ErrorMessage;
                return "Confira este campo. Informe um valor válido no formato esperado.";
            }).Distinct().ToArray());
        var problema = new ValidationProblemDetails(campos)
        {
            Status = 400, Title = "Confira os dados informados",
            Detail = "Corrija os campos indicados e envie novamente.", Instance = action.HttpContext.Request.Path
        };
        problema.Extensions["code"] = "requisicao_invalida";
        problema.Extensions["traceId"] = System.Diagnostics.Activity.Current?.Id ?? action.HttpContext.TraceIdentifier;
        var resultado = new BadRequestObjectResult(problema);
        resultado.ContentTypes.Add("application/problem+json");
        return resultado;
    }
}
