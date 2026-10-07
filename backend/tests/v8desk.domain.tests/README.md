# Regressões do domínio

Execute com .NET 10:

```powershell
dotnet run --project backend/tests/v8desk.domain.tests/v8desk.domain.tests.csproj
```

O executor retorna código 1 se qualquer cenário falhar. Não usa framework de testes externo; `dotnet test` não executa esta suíte. O projeto referencia a API para verificar seu contrato de erros (e usa as dependências já presentes nela). Os cenários cobrem validações, cronologia, limites, acesso restrito, isolamento entre empresas, respostas HTTP, calendário histórico, revogação de acesso, transferências, encapsulamento, categorias/subcategorias, prazo de avaliação e pausa/retomada dos SLAs.

## Integração PostgreSQL (opcional)

Defina `V8DESK_POSTGRES_TEST_CONNECTION` no ambiente local apontando para um banco exclusivo com nome iniciado por `v8desk_test`. Execute o mesmo runner. A suíte cria um schema aleatório, aplica migrations e testa persistência, concorrência, rollback, permissões, idempotência e outbox. O schema fica preservado para inspeção. Sem a variável, nenhuma conexão é aberta e a saída informa que a integração não foi executada. Não use banco de produção nem versione a conexão.
