using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using v8desk.api.Errors;
using v8desk.domain.Exceptions;

namespace v8desk.domain.tests;

public static class ApiErrorsTests
{
    public static void ErrosConhecidosSaoAmigaveisEFalhasInternasNaoVazamDetalhes()
    {
        foreach (var (erro, status, codigo) in new (Exception, int, string)[]
        {
            (new ValidacaoDominioException("nota_invalida", "nota", "Escolha uma nota de 1 a 5."), 400, "nota_invalida"),
            (new RegraNegocioException("O chamado já possui responsável."), 409, "regra_negocio"),
            (new AcessoNegadoException("Você não tem acesso a este chamado."), 403, "acesso_negado"),
            (new InvalidOperationException("Senha=segredo; servidor=interno"), 500, "erro_inesperado")
        })
        {
            using var servicos = new ServiceCollection().AddOptions().BuildServiceProvider();
            var contexto = new DefaultHttpContext { RequestServices = servicos, TraceIdentifier = "teste-123" };
            using var body = new MemoryStream();
            contexto.Response.Body = body;
            var handler = new ApiExceptionHandler(NullLogger<ApiExceptionHandler>.Instance);
            Assert.True(handler.TryHandleAsync(contexto, erro, CancellationToken.None).AsTask().GetAwaiter().GetResult());
            Assert.Equal(status, contexto.Response.StatusCode);
            Assert.True(contexto.Response.ContentType!.StartsWith("application/problem+json"));
            using var json = JsonDocument.Parse(body.ToArray());
            Assert.Equal(codigo, json.RootElement.GetProperty("code").GetString());
            Assert.Equal("teste-123", json.RootElement.GetProperty("traceId").GetString());
            if (status == 500) Assert.False(json.RootElement.GetProperty("detail").GetString()!.Contains("segredo"));
            if (status == 400) Assert.True(json.RootElement.GetProperty("errors").TryGetProperty("nota", out _));
        }
    }
}
