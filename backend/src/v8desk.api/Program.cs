var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<v8desk.application.Abstractions.IEmpresaAtual, v8desk.api.Tenancy.EmpresaAtualHttp>();
builder.Services.AddScoped<v8desk.application.Abstractions.IUsuarioAtual, v8desk.api.Tenancy.EmpresaAtualHttp>();
builder.Services.AddScoped<v8desk.application.Abstractions.IAutorizacaoEmpresa, v8desk.api.Tenancy.AutorizacaoEmpresaHttp>();
var conexao = builder.Configuration.GetConnectionString("V8Desk");
if (!string.IsNullOrWhiteSpace(conexao))
{
    v8desk.application.DependencyInjection.AdicionarAplicacao(builder.Services);
    v8desk.infrastructure.DependencyInjection.AdicionarInfraestrutura(builder.Services, conexao);
}
else if (!builder.Environment.IsDevelopment())
    throw new InvalidOperationException("Configure ConnectionStrings:V8Desk antes de iniciar o servidor.");

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddExceptionHandler<v8desk.api.Errors.ApiExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.Configure<Microsoft.AspNetCore.Mvc.ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = action =>
    {
        var campos = action.ModelState.Where(item => item.Value?.Errors.Count > 0)
            .ToDictionary(item => item.Key,
                _ => new[] { "Confira este campo. O valor está ausente ou não tem o formato esperado." });
        var problema = new Microsoft.AspNetCore.Mvc.ValidationProblemDetails(campos)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Confira os dados informados",
            Detail = "Corrija os campos indicados e envie novamente."
        };
        problema.Extensions["code"] = "requisicao_invalida";
        problema.Extensions["traceId"] = action.HttpContext.TraceIdentifier;
        var resultado = new Microsoft.AspNetCore.Mvc.BadRequestObjectResult(problema);
        resultado.ContentTypes.Add("application/problem+json");
        return resultado;
    };
});
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();
app.UseExceptionHandler();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
