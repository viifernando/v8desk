# Correções e contratos do domínio

- `CalendarioEmpresa.CriarSnapshot()` preserva identificação, versão, expediente, feriados e fuso, bloqueando edições. SLAs e períodos calculam com esse snapshot, sem receber um calendário mutável do chamador. Na persistência, esses snapshots devem ser gravados e reconstituídos; o domínio sozinho não oferece armazenamento.
- `VinculoSetor` recebe `Usuario`, e `MembroFila` referencia o vínculo. Permissões verificam usuário ativo, vínculo ativo e papel de atendente em cada consulta. Reconstituir essas relações usando o estado atual, sem cópias desatualizadas.
- A primeira resposta permanece global desde a abertura mesmo com transferências. Os contadores locais de resolução/próxima resposta continuam separados. Alterar a prioridade no destino não aplica sua política à primeira resposta global do setor de abertura.
- `DadosAbertura.Categoria` recebe `ReferenciaHistorica`. Nome e identificador são preservados nos eventos de abertura, troca e transferência. A aplicação deve carregar a categoria real e validar seu setor antes da abertura.
- Payloads de eventos são cópias imutáveis; papéis são expostos em cópia somente leitura.
- `PoliticaCicloVida` exige `PrazoAvaliacao`. O padrão inicial adotado é sete dias corridos, configurável. Cada ciclo grava o limite ao encerrar; `Chamado.Avaliar` não aceita mais uma data limite externa.
- A API de cálculo passa a usar `sla.CalcularConsumido(agora)` e `periodo.TempoUtil(agora)`.
- A alteração preexistente em `TrocarResponsavel` foi preservada.

## Verificação

## Categorias e subcategorias

- Uma categoria pode ter pai e filhas em qualquer profundidade. O setor cria e move categorias, rejeitando ciclos, pais de outros setores e nomes duplicados entre irmãos. Nomes iguais em ramos diferentes são permitidos.
- `Setor.ConfigurarRecebimentoCategoria` determina se uma categoria com filhas ativas pode receber chamados. Por padrão pode; o setor pode exigir a seleção de uma subcategoria. Ancestral inativo bloqueia novas seleções de todo o ramo.
- Fila de destino ausente herda dos pais; sem destino na hierarquia, usa a geral. Configurar explicitamente a fila geral interrompe a herança; `null` restaura a herança. Destino desativado usa a geral.
- A pré-qualificação usa a primeira lista não vazia da categoria até a raiz. A lista própria substitui a herdada. Ausência em todo o ramo disponibiliza o chamado à equipe autorizada da fila. A autorização continua independente.
- `DadosAbertura.Categoria` agora recebe a entidade Categoria. O chamado valida que pode ser selecionada e captura `CaminhoCategoria`, com identificadores e nomes de todos os níveis. Alteração e transferência também capturam o caminho. Renomeações e movimentações não modificam o prontuário antigo.
- A aplicação deve determinar a fila de abertura com `Setor.DeterminarFilaInicial`. Mudanças na hierarquia/configurações não transferem chamados existentes. As etapas de atendimento permanecem as definidas anteriormente.
- A política de visibilidade por categoria permanece explícita, sem herança automática nesta alteração.

A suíte executável em `tests/v8desk.domain.tests` usa `dotnet run`, sem dependências externas. Infraestrutura ainda precisa implementar autorização das consultas, armazenamento histórico e controle otimista de concorrência com `Chamado.Versao`.
