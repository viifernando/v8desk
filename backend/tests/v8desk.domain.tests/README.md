# Regressões do domínio

Execute com .NET 10:

```powershell
dotnet run --project backend/tests/v8desk.domain.tests/v8desk.domain.tests.csproj
```

O executor retorna código 1 se qualquer cenário falhar. Não usa pacotes externos; `dotnet test` não executa esta suíte. Os cenários cobrem calendário histórico, revogação de acesso, transferência antes da primeira resposta, encapsulamento, nomes de categorias, prazo de avaliação e pausa/retomada dos SLAs.
