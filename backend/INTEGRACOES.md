# Integrações de e-mail e Microsoft

Implementação do backend, sem alteração do frontend. Configuração por empresa, SMTP com TLS ou envio pelo Microsoft Graph, autenticação Microsoft Entra vinculada a usuários internos e processamento de notificações em segundo plano. O Graph de envio e o Entra de login têm configurações independentes.

## Instalação

Aplicar as migrations pelo processo de implantação, inclusive `20261007124455_IntegracoesEmailMicrosoft`. A API não aplica migrations nem inicia PostgreSQL automaticamente.

Configuração do servidor, por variáveis de ambiente ou configuração externa:

```text
ConnectionStrings__V8Desk=<conexão PostgreSQL>
Integrations__WorkerEnabled=true
Integrations__SmtpHostsPermitidos__0=smtp.suaempresa.com
Integrations__DataProtectionKeysPath=<diretório persistente de chaves>
Authentication__EntraEnabled=true
```

`WorkerEnabled` e `EntraEnabled` começam desativados. Habilitar somente os recursos desejados. A lista SMTP é definida pelo administrador da instalação e restringe os destinos permitidos, independentemente da configuração enviada pela empresa. SMTP exige StartTLS ou TLS implícito, com validação normal do certificado.

Em produção, o diretório de chaves é obrigatório. Windows protege as chaves com DPAPI; em Linux é obrigatório configurar `Integrations__DataProtectionCertificatePath` com certificado PKCS#12 e fornecer `Integrations__DataProtectionCertificatePassword` por armazenamento seguro. Instâncias que compartilham o banco precisam conseguir descriptografar as mesmas chaves: planejar volume compartilhado, certificado comum e identidade de execução compatível. DPAPI local não serve sozinho para compartilhar chaves entre máquinas. Preservar as chaves e o certificado nos backups; perder ambos exige cadastrar novamente os segredos das integrações.

Senhas SMTP e segredos Graph são protegidos com ASP.NET Data Protection, com finalidade separada por empresa. Não aparecem nas respostas, auditorias ou mensagens de erro. As respostas informam apenas `temCredencial`. Não registrar corpos desses endpoints no proxy ou em ferramentas de observabilidade.

## Endpoints

Os endpoints administrativos exigem autenticação, empresa válida, usuário interno ativo e papel interno `administrador_empresa`. Operações de escrita exigem `Idempotency-Key`; diagnóstico não altera a configuração e dispensa essa chave.

| Método | Caminho | Finalidade |
| --- | --- | --- |
| GET | `/api/v1/integracoes` | Configuração sem segredos e indicador `processamentoAtivo` do servidor |
| POST | `/api/v1/integracoes/email` | Configurar SMTP ou Graph |
| POST | `/api/v1/integracoes/email/diagnostico` | Validar conexão/autenticação sem enviar mensagem |
| POST | `/api/v1/integracoes/email/testar` | Agendar teste para o próprio administrador; retorna 202 |
| GET | `/api/v1/integracoes/email/entregas?tamanho=25` | Últimas entregas, de 1 a 100, com situação e mensagem amigável |
| POST | `/api/v1/integracoes/email/ativacao` | Ativar ou pausar, corpo `{ "ativo": true }` |
| POST | `/api/v1/integracoes/email/entregas/{id}/repetir` | Reagendar uma entrega em situação `Falha` |
| POST | `/api/v1/integracoes/usuarios/{usuarioId}/email` | Cadastrar contato de notificações, corpo `{ "email": "pessoa@empresa.com" }` |
| POST | `/api/v1/integracoes/microsoft` | Configurar tenant e aplicativos Microsoft |
| POST | `/api/v1/integracoes/usuarios/{usuarioId}/microsoft` | Associar conta Microsoft e autorização administrativa interna |
| POST | `/api/v1/integracoes/usuarios/{usuarioId}/microsoft/desativar` | Desativar associação, corpo `{ "objetoId": "<GUID>" }` |
| POST | `/api/v1/integracoes/microsoft/ativacao` | Ativar ou pausar autenticação Microsoft |
| GET | `/api/v1/acesso/microsoft/{empresaId}` | Descoberta pública de authority, cliente e escopo, somente quando ativado |

Consultar `src/v8desk.api/v8desk.api.http` para exemplos. Valores GUID dos exemplos são ilustrativos.

## Configurar e-mail

1. Cadastrar o e-mail do administrador que fará o teste.
2. Configurar o provedor. Um `segredo` omitido conserva a credencial somente se a identidade do provedor continuar igual; trocar host/usuário SMTP ou tenant/aplicativo Graph exige cadastrar a nova credencial.
3. Executar o diagnóstico. No SMTP, testa conexão e autenticação; no Graph, testa obtenção de token. O diagnóstico Graph sozinho não confirma permissão de envio nem disponibilidade da caixa.
4. Solicitar o teste e consultar entregas até `AceitaPeloProvedor`. O worker deve estar habilitado no servidor; a API avisa quando o processamento está pausado.
5. Ativar notificações. Somente um teste aceito da revisão atual permite ativar. Alterar a configuração pausa o envio e invalida o teste anterior. A validade opcional da credencial é verificada antes de ativar e enviar.

SMTP usa `provedor: "Smtp"`, remetente, nome, host, porta, `segurancaSmtp: "StartTls"` ou `"TlsImplicito"`, usuário e segredo. Um relay sem usuário pode dispensar credencial, mantendo TLS obrigatório.

Graph usa `provedor: "MicrosoftGraph"`, remetente da caixa, nome, `tenantGraphId`, `clienteGraphId`, `segredo` e opcional `credencialExpiraEm`. O aplicativo usa client credentials; o segredo pertence à integração de envio, não ao login do usuário. Configurar acesso de envio por aplicação e consentimento administrativo no ambiente Microsoft. Restringir o acesso à caixa necessária, usando os mecanismos do Exchange adequados à implantação. Permissões amplas concedidas no Entra podem somar-se às permissões do Application RBAC; uma atribuição restrita no Exchange não remove sozinha uma concessão ampla existente. Consultar [Application RBAC](https://learn.microsoft.com/en-us/exchange/permissions-exo/application-rbac) e [sendMail](https://learn.microsoft.com/en-us/graph/api/user-sendmail?view=graph-rest-1.0).

## Configurar login Microsoft

Registrar um aplicativo da API no Entra, com access tokens v2 e escopo delegado `access_as_user`, usando Application ID URI `api://<apiClienteId>`. Registrar também o aplicativo cliente do login, configurar suas URIs de redirecionamento no Entra e autorizar acesso ao escopo da API. A implementação atual aceita a nuvem pública Microsoft.

Cadastrar `tenantId`, `apiClienteId` e `clienteLoginId` no endpoint Microsoft. Associar cada usuário interno usando o Object ID da conta naquele tenant e `administrador` conforme a autorização desejada. Ativar a integração após estabelecer as associações. Existe uma configuração por empresa, e o par tenant/aplicativo da API identifica uma única empresa; compartilhar tenant entre empresas exige aplicativos da API distintos.

O cliente deverá implementar o login com MSAL/authorization code e PKCE e enviar o access token da API em `Authorization: Bearer`. O frontend/login interativo ainda não faz parte desta entrega. A API valida assinatura RS256, issuer, audience, expiração, tenant, Object ID, versão, escopo delegado e aplicativo cliente autorizado (`azp`). Tokens do Graph, ID tokens e tokens de aplicação sem escopo delegado não autenticam usuários.

A identidade final e o papel administrativo vêm da associação no V8Desk; roles, e-mail e identificadores internos enviados pelo token externo não concedem privilégios. Nenhum usuário é criado automaticamente por coincidência de e-mail. Desativar usuário, associação ou integração bloqueia novas autenticações. A associação inicial exige um administrador já autenticado por um provedor confiável ou provisionamento operacional; não existe endpoint público de autoatribuição administrativa. Referência: [claims de access tokens](https://learn.microsoft.com/en-us/entra/identity-platform/access-token-claims-reference).

## Processamento e limites

Eventos do chamado entram na outbox transacional; o publicador converte eventos em entregas persistidas antes de acessar o provedor. Workers usam bloqueio de linha `FOR UPDATE SKIP LOCKED`, lotes limitados e transações independentes por entrega. O envio externo não participa da repetição automática de comandos do EF.

Notificações se destinam ao solicitante e ao responsável atual, evitando notificar o autor da própria interação, exceto a confirmação de abertura. Antes de enviar, o worker verifica conta ativa, contato e acesso atual ao chamado. Notas internas, correções de mensagens e anexos não geram e-mail. As mensagens contêm apenas aviso genérico e número do chamado, sem título, descrição ou conteúdo do prontuário. A entrega usa a configuração e contato atuais. Pausar notificações faz os eventos e entregas normais processados durante a pausa serem ignorados; não há reprodução automática de todo o período pausado.

Falhas temporárias agendam nova tentativa; Graph 429 respeita `Retry-After`, limitado entre 1 segundo e 24 horas. Sem essa indicação, há espera exponencial de até uma hora. Após 10 tentativas, ou erro permanente, a entrega fica em `Falha`; o administrador pode reagendá-la após corrigir a causa. O prazo de cada tentativa do worker é 30 segundos. IDs estáveis identificam a entrega no Message-ID SMTP ou cabeçalho Graph.

`AceitaPeloProvedor` confirma aceitação do envio, sem garantir entrega na caixa. A estratégia permite repetição após falha entre aceitação externa e confirmação da transação no banco; nesse intervalo, mensagens duplicadas são possíveis. Os IDs permitem rastreamento, mas não representam uma garantia de envio exatamente uma vez. Ver [throttling do Graph](https://learn.microsoft.com/en-us/graph/throttling).

Há auditoria imutável do ator, tipo de alteração e horário, sem registrar valores de credenciais. Ainda não estão incluídos: recebimento de e-mail para criar/responder chamados, sincronização de usuários/grupos, distribuição por toda a fila, UI de configuração/login, confirmação de leitura, webhooks de entrega ou encerramento periódico de chamados.

## Verificação

```powershell
dotnet build backend/v8desk.slnx
dotnet run --project backend/tests/v8desk.domain.tests
```

Os testes locais cobrem configuração/revisão, credenciais por empresa, autorização administrativa, idempotência, testes de envio, TLS, erros amigáveis, Graph 429/403/404/503, retentativas, processamento, revogação de acesso e validação criptográfica real de tokens Entra com metadados controlados.

O teste real de PostgreSQL é opcional, exige `V8DESK_POSTGRES_TEST_CONNECTION` apontando para banco exclusivo cujo nome começa com `v8desk_test`, cria schema isolado e o preserva para inspeção. Também verifica persistência das integrações, filtros, descoberta de identidade e processamento de entregas com transporte simulado. Não inicia Docker nem envia mensagens reais. A validação operacional com SMTP/Graph, consentimento Microsoft e PostgreSQL reais deve acontecer no ambiente de homologação.
