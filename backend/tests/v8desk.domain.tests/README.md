# Regressões do domínio

Execute com .NET 10:

```powershell
dotnet run --project backend/tests/v8desk.domain.tests/v8desk.domain.tests.csproj
```

O executor retorna código 1 se qualquer cenário falhar. Não usa framework de testes externo; `dotnet test` não executa esta suíte. O projeto referencia a API para verificar seu contrato de erros (e usa as dependências já presentes nela). Os cenários cobrem validações, cronologia, limites, acesso restrito, isolamento entre empresas, respostas HTTP, calendário histórico, revogação de acesso, transferências, encapsulamento, categorias/subcategorias, prazo de avaliação e pausa/retomada dos SLAs.
