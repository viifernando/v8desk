# API V8Desk

A API expõe comandos da Application e consultas paginadas. Os controllers recebem DTOs HTTP, usam a identidade autenticada e propagam cancelamento. Não recebem entidades, permissões, empresa, autor ou horário no corpo da operação.

## Contrato

- Base: `/api/v1`.
- JSON em camelCase. Enumerações são nomes, como `Media`, `CompartilhadoComSetor` e `PrazoMaisProximo`; números em enumerações JSON são rejeitados.
- Corpo limitado a 128 KiB; profundidade JSON máxima de 32. Campos desconhecidos e campos obrigatórios ausentes são rejeitados.
- GUIDs vazios são rejeitados. Campos anuláveis que removem uma configuração devem ser enviados explicitamente como `null`; omitir `usuarioId`, `paiId`, `filaId` ou `limite` em uma alteração não significa removê-los.
- Escritas exigem exatamente um cabeçalho `Idempotency-Key`, não vazio e de até 200 caracteres. Gere uma chave por intenção de operação e preserve-a nas tentativas da mesma operação. Empresa, usuário, tipo de comando e parâmetros participam da proteção de idempotência.
- A abertura retorna `201 Created`, com `Location` para o detalhe do chamado. O replay preserva esse resultado. Os demais comandos retornam `200` com identificação e resultado tipado. Não há alteração arbitrária de status via PATCH.
- Respostas têm `Cache-Control: no-store`, `X-Content-Type-Options: nosniff` e `X-Request-Id`.

## Endpoints

| Método e endereço | Uso |
| --- | --- |
| POST /api/v1/chamados | Abrir chamado |
| GET /api/v1/chamados | Pesquisar por fila, situação, prioridade, responsável, categoria, datas e texto |
| GET /api/v1/chamados/{id} | Detalhe, contexto, categoria, prazos e versão |
| GET /api/v1/chamados/{id}/mensagens | Mensagens paginadas e texto atual corrigido |
| GET /api/v1/chamados/{id}/historico | Cabeçalhos dos eventos, paginados por sequência |
| POST /api/v1/chamados/{id}/aceitar | Aceitar |
| POST /api/v1/chamados/{id}/assumir | Assumir |
| POST /api/v1/chamados/{id}/iniciar-atendimento | Iniciar atendimento |
| POST /api/v1/chamados/{id}/responsavel | Trocar ou remover responsável com motivo |
| POST /api/v1/chamados/{id}/solicitar-informacao | Solicitar informação |
| POST /api/v1/chamados/{id}/respostas/solicitante | Responder como solicitante |
| POST /api/v1/chamados/{id}/respostas/equipe | Responder como equipe |
| POST /api/v1/chamados/{id}/notas-internas | Registrar nota interna |
| POST /api/v1/chamados/{id}/prioridade | Alterar prioridade |
| POST /api/v1/chamados/{id}/categoria | Alterar categoria |
| POST /api/v1/chamados/{id}/transferir | Transferir |
| POST /api/v1/chamados/{id}/visibilidade | Alterar visibilidade |
| POST /api/v1/chamados/{id}/resolver | Resolver |
| POST /api/v1/chamados/{id}/confirmar-solucao | Confirmar solução |
| POST /api/v1/chamados/{id}/rejeitar-solucao | Informar solução insuficiente |
| POST /api/v1/chamados/{id}/reabrir | Reabrir |
| POST /api/v1/chamados/{id}/cancelar | Cancelar com motivo |
| POST /api/v1/chamados/{id}/avaliacao | Avaliar, com comentário opcional e regra de nota baixa |
| POST /api/v1/chamados/{id}/mensagens/{mensagemId}/correcao | Corrigir mensagem preservando o original |
| POST /api/v1/minha-conta/disponibilidade | Definir disponibilidade pessoal |
| POST /api/v1/setores/{setorId}/… | Configurar setor como Gestor |
| POST /api/v1/empresa/… | Configurar empresa como administrador |
| GET /health/live | Verificar processo |
| GET /health/ready | Verificar configuração e conexão PostgreSQL |
| GET /openapi/v1.json | Documento OpenAPI, somente em Development |

Os controllers de setores cobrem nome, desativação, filas, categorias, hierarquia, recebimento, destino, visibilidade, pré-qualificação, metas SLA, ciclo de vida, acesso restrito, membros, vínculos e papéis. `ciclo-vida/herdar` remove a política específica do setor. Os controllers da empresa cobrem nome, criação de setores e usuários, administração de usuários, limite de setores, ciclo de vida, fuso, expediente e exceções. O documento OpenAPI apresenta os endereços e DTOs individuais.

`solicitar-informacao`, `resolver`, respostas e notas recebem `{ "texto": "…" }`. Cancelamento, reabertura e rejeição recebem `{ "motivo": "…" }`. O arquivo `src/v8desk.api/v8desk.api.http` contém exemplos.

### Paginação e prontuário

Listagens e mensagens usam `tamanho` de 1 a 100, padrão 25, e `cursor` retornado pela página anterior. A busca exige `filaId`; o total é limitado a 10.000 e informa se excedeu esse limite. O histórico usa `depois` (sequência, inicialmente zero) e devolve `proximaSequencia`.

As leituras autorizam e consultam dentro do mesmo snapshot transacional. O detalhe é uma projeção, sem carregar todo o agregado. Mensagens e eventos são limitados no SQL. Solicitantes e colegas não recebem notas internas, nem eventos de nota ou correção que possam revelar atividade interna. Atendentes autorizados na fila atual recebem essas informações. O histórico público fornece cabeçalhos; os dicionários livres dos eventos, motivos internos, correções anteriores e chaves de armazenamento de anexos não são expostos por esta consulta. O prontuário completo continua persistido.

## Mensagens e erros

Erros retornam `application/problem+json`, com mensagem em português, `code`, `traceId` e `instance`. Validações incluem `errors` por campo. A interface pode exibir `detail` e destacar os campos, usando `code` para decisões e `traceId` para suporte. Valores inválidos, SQL, detalhes de token e mensagens de exceções inesperadas não são devolvidos.

```json
{
  "status": 400,
  "title": "Confira os dados informados",
  "detail": "Corrija os campos indicados e envie novamente.",
  "code": "requisicao_invalida",
  "traceId": "identificador-do-atendimento",
  "instance": "/api/v1/chamados",
  "errors": { "Titulo": ["Informe o título."] }
}
```

| Status | Tratamento esperado |
| --- | --- |
| 200 | Consulta ou alteração concluída, com resultado JSON |
| 201 | Chamado, setor, usuário, fila, categoria ou vínculo criado, com ID no corpo |
| 202 | Teste de e-mail agendado; consultar a fila de entregas para acompanhar |
| 400 | Corrigir campos, formato ou chave de operação |
| 401 | Obter sessão válida; inclui WWW-Authenticate: Bearer |
| 403 | Informar falta de permissão; recursos inexistentes e inacessíveis não são distinguidos |
| 404 / 405 | Conferir endereço ou método HTTP |
| 409 | Informar regra de negócio, conflito de versão ou chave reutilizada com outros dados |
| 413 / 415 | Reduzir corpo ou enviar JSON |
| 429 | Respeitar Retry-After |
| 503 | Serviço ou dependência indisponível; respeitar Retry-After quando presente |
| 500 | Mensagem genérica; informar traceId ao suporte |

Os endpoints de criação preservam a resposta 201 e o mesmo corpo no replay com a mesma `Idempotency-Key`. A abertura de chamado inclui `Location` para a consulta do chamado. As demais criações ainda não têm GET de detalhe próprio e retornam 201 com o identificador, sem `Location`. Operações sobre recursos existentes continuam retornando 200 com o resultado. O OpenAPI declara explicitamente 201/202 onde aplicável e as respostas de erro compartilhadas; a descoberta pública Microsoft também declara 404 quando indisponível.

## Autenticação e empresa

O host valida JWT de um provedor OIDC/OAuth configurado por `Authentication:Authority` e `Authentication:Audience`. Valida assinatura, emissor, público e expiração; aceita até 30 segundos de diferença de relógio. Metadados exigem HTTPS.

O token deve conter `sub` com o GUID do usuário V8Desk, `empresa_id` com o GUID da empresa e, para administração da empresa, `role: administrador_empresa`. Vínculos, atividade da conta e permissões do chamado continuam sendo verificados nos dados persistidos. Se o provedor usar um identificador externo em `sub`, será necessário um adaptador de mapeamento para o usuário V8Desk antes de conectá-lo.

Em produção, configuração de banco e autenticação ausente impede a inicialização. Em Development sem provedor, o serviço inicia para desenvolvimento, mas os endpoints privados continuam retornando 401; cabeçalhos de empresa/usuário não autenticam ninguém. Sem banco configurado, uma operação privada autenticada retorna 503. Não foram criados login, emissão de tokens ou usuários administrativos fictícios.

Configure por variáveis de ambiente ou armazenamento de segredos do host:

- `ConnectionStrings__V8Desk`: conexão PostgreSQL.
- `Authentication__Authority`: endereço HTTPS do provedor.
- `Authentication__Audience`: identificador da API no provedor.
- `AllowedHosts`: nomes de host aceitos na implantação.

## Resiliência e implantação

Padrões configuráveis: 120 requisições por minuto por empresa/usuário, 64 requisições simultâneas no processo e prazo de 60 segundos. Rejeições não entram em uma fila de espera. O prazo cancela o token propagado à Application e às consultas; não mata trabalho que ignore cancelamento. Não há retry de escrita no controller: as tentativas transacionais pertencem ao executor idempotente da infraestrutura.

Uma operação pode ter sido concluída quando a conexão caiu ou o prazo acabou. O cliente deve tentar novamente com a mesma chave e o mesmo conteúdo. Nunca gere outra chave automaticamente para esse retry.

Os limites são locais à instância. Em múltiplas réplicas, a política agregada deve ser aplicada também pelo gateway. `ReverseProxy:KnownProxies` aceita IPs explícitos de proxies confiáveis; somente nesses casos são processados X-Forwarded-For e X-Forwarded-Proto, com um salto. Configure conforme a topologia real. CORS amplo não é habilitado.

Saúde não recebe identidade de empresa e não expõe conexão ou erros de infraestrutura. Readiness verifica conexão com prazo de três segundos e configuração do provedor; não verifica migrations, alcance do provedor OIDC nem entrega de notificações. Migrations continuam sendo uma etapa de implantação, sem atualização automática ao iniciar a API.

## Verificação e próximos incrementos

```powershell
dotnet run --project backend/tests/v8desk.domain.tests
dotnet run --project backend/src/v8desk.api --launch-profile https
```

Os testes HTTP usam o host real e substituem persistência por doubles. Verificam abertura e Location, replay, conflitos, validação e JSON, 401/403, campos de identidade rejeitados, isolamento de empresa, corpo/formato, erros 500/503, limite 429, prazo/cancelamento, saúde e geração de OpenAPI. JWT é testado com assinatura RSA real e chaves locais somente no teste, sem acessar um provedor externo. As novas consultas têm testes de tradução SQL; o teste PostgreSQL opt-in inclui paginação e proteção de notas.

As integrações SMTP/Graph, configuração administrativa por empresa, fila de notificações e autenticação Microsoft Entra estão documentadas em `INTEGRACOES.md`. O processamento é opcional e começa desativado no servidor. Não há endpoint público que execute o worker.

PostgreSQL real, carga e integração com provedores de produção ainda precisam ser executados. Permanecem para próximos incrementos: provisionamento inicial de empresa/identidade, consultas de diretórios/configurações para formulários, relatórios de métricas, revisão completa do prontuário para gestores, armazenamento/download de anexos, login interativo no frontend, sincronização de diretórios, recebimento de e-mails e execução periódica do encerramento automático.

Referências de implementação: [autenticação JWT](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/configure-jwt-bearer-authentication?view=aspnetcore-10.0), [limitação de requisições](https://learn.microsoft.com/en-us/aspnet/core/performance/rate-limit?view=aspnetcore-10.0) e [prazos de requisição](https://learn.microsoft.com/en-us/aspnet/core/performance/timeouts?view=aspnetcore-10.0).
