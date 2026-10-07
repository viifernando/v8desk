# Persistência do V8Desk

Base configurada para .NET 10, EF Core 10 e Npgsql 10, com PostgreSQL 17 no Compose. Nenhum banco é iniciado pela aplicação e nenhuma migration é aplicada automaticamente. O domínio continua sem dependência do EF: construtores de materialização e setters privados preservam os métodos e as validações de negócio.

## Organização e integridade

- `v8desk.application/Abstractions`: identidade da empresa, repositório de chamados e unidade de trabalho.
- `v8desk.infrastructure/Persistence`: contexto, mapeamentos, serialização de snapshots, migrations e outbox.
- `v8desk.infrastructure/Repositories`: carregamento para comandos e projeções para consultas.
- `v8desk.api/Tenancy`: empresa obtida da claim autenticada `empresa_id`. Autenticação ainda precisa ser implementada; a empresa não é aceita de um header enviado pelo cliente.

Todas as entidades têm filtro por empresa. Chaves e relacionamentos compostos incluem a empresa, e a gravação rejeita registros de outra empresa. Isso complementa a autorização de negócio: filtros de empresa não substituem permissões de setor, fila e visibilidade. IDs e permissões usados pelas consultas precisam vir da identidade e da autorização verificadas. Não exponha os argumentos de autorização diretamente como parâmetros de uma API. `IgnoreQueryFilters` exige um fluxo administrativo autorizado; não há RLS configurada neste estágio.

As entidades usam `xmin` para concorrência otimista; o chamado também possui versão do domínio. Uma disputa gera mensagem amigável para atualizar a tela. Eventos são sequenciais e sua alteração/exclusão é bloqueada pelo contexto. Registros históricos não são excluídos pelo EF; membros de fila podem ser removidos. Esses controles não bloqueiam alterações feitas diretamente por um administrador do banco.

Entidades e relações ficam em tabelas. Calendários versionados, caminhos de categorias, contextos históricos, correções e demais valores pertencentes aos agregados ficam em JSONB, com comparação profunda para detectar mudanças. Metadados dos anexos ficam no banco; conteúdo binário exige um serviço de armazenamento, ainda não implementado. Datas são gravadas em UTC, preservando o fuso de negócio no calendário. Metas usam `numeric` para preservar precisão.

## Consultas e resiliência

Consultas são sem tracking por padrão, com projeção e paginação por cursor limitada a 100 itens. Há índices de fila, responsável, validação de resolução, eventos, mensagens, etapas e SLA. Colunas geradas extraem os IDs do contexto JSONB para filtrar e indexar sem duplicar sua manutenção no domínio.

O repositório de comandos carrega o ciclo mais recente, períodos e SLAs ativos, e opcionalmente uma mensagem específica. Ele **não representa o prontuário completo**. Uma futura tela de histórico deve consultar eventos, mensagens e ciclos com paginação própria. As coleções parcialmente carregadas não devem ser usadas para produzir um histórico completo.

O contexto tem escopo por operação, sem pooling de contextos para não reutilizar a identidade da empresa. O pool de conexões do Npgsql continua disponível. O provedor tem três tentativas para falhas transitórias e timeout de comando de 30 segundos. Transações explícitas futuras devem usar a estratégia de execução do EF; operações externas não devem entrar nessas tentativas.

Eventos novos geram registros de outbox com o mesmo ID, gravados na mesma transação do chamado. **O dispatcher ainda não existe**: notificações e integrações não são enviadas neste estágio. Antes de expor comandos, implemente idempotência das requisições; retries de transporte não substituem esse mecanismo.

Índices dos períodos e SLAs ativos não são únicos: trocar etapas pode inserir uma nova linha antes de atualizar a antiga na mesma gravação. A invariante é protegida pelo agregado e sua concorrência otimista, evitando conflito artificial de ordem de SQL.

## Configuração e execução futura

Execute os comandos a partir de `backend`. Instale o SDK .NET 10 e restaure a ferramenta local:

```powershell
dotnet tool restore
```

Quando for hora de subir o banco, copie `.env.example` para `.env`, escolha uma senha local e execute:

```powershell
docker compose up -d postgres
```

Configure `ConnectionStrings__V8Desk` no ambiente do processo da API, ou `ConnectionStrings:V8Desk` via user-secrets. Formato (substitua os valores localmente):

```text
Host=localhost;Port=5432;Database=v8desk;Username=v8desk;Password=<senha-local>
```

Não versione credenciais. Em desenvolvimento a API pode iniciar sem persistência quando a conexão não está configurada; nos demais ambientes ela exige configuração. A factory de design usa uma empresa fictícia somente para gerar migrations e não exige conexão para geração de SQL.

Gere um script revisável sem acessar o banco:

```powershell
dotnet tool run dotnet-ef migrations script --idempotent --project src/v8desk.infrastructure --startup-project src/v8desk.infrastructure --output migrations.sql
```

Depois que o banco estiver disponível, aplique as migrations em desenvolvimento:

```powershell
dotnet tool run dotnet-ef database update --project src/v8desk.infrastructure --startup-project src/v8desk.infrastructure
```

Em produção use um processo de implantação controlado e uma credencial própria para migrations. O usuário padrão do Compose é apenas para desenvolvimento. Backup, restauração, TLS, monitoramento, testes de carga e worker de outbox são etapas posteriores.

## Verificação atual

```powershell
dotnet build v8desk.slnx
dotnet run --project tests/v8desk.domain.tests
dotnet tool run dotnet-ef migrations has-pending-model-changes --project src/v8desk.infrastructure --startup-project src/v8desk.infrastructure
```

Os testes verificam regras de domínio, mensagens da API, modelo e geração de SQL, isolamento entre empresas, serialização de snapshots e preparação do grafo/outbox. Não substituem testes de integração com PostgreSQL real. Antes de implantar, valide round-trip dos agregados, concorrência em duas conexões, rollback, migrations e processamento da outbox no banco real.

Referências: [Npgsql EF Core](https://www.npgsql.org/efcore/), [concorrência no EF Core](https://learn.microsoft.com/en-us/ef/core/saving/concurrency), [resiliência de conexões](https://learn.microsoft.com/en-us/ef/core/miscellaneous/connection-resiliency) e [filtros globais](https://learn.microsoft.com/en-us/ef/core/querying/filters).
