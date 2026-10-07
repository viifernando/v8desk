using v8desk.domain.Enums;
using v8desk.domain.ValueObjects;

namespace v8desk.application.Chamados;

public sealed record ChamadoDetalhe(Guid Id, long Numero, string Titulo, string Descricao,
    Guid SolicitanteId, StatusChamado Status, Prioridade Prioridade, Visibilidade Visibilidade,
    ReferenciaHistorica SetorOrigem, CaminhoCategoria Categoria, ContextoAtendimento Contexto,
    DateTimeOffset AbertoEm, DateTimeOffset AtualizadoEm, DateTimeOffset? ProximoVencimentoEm,
    DateTimeOffset? LimiteValidacao, DateTimeOffset? LimiteReabertura, long Versao);
public sealed record MensagemLinha(Guid Id, Guid AutorId, TipoMensagem Tipo, string Texto,
    DateTimeOffset CriadaEm, bool Corrigida, int QuantidadeAnexos);
public sealed record PaginaMensagens(IReadOnlyList<MensagemLinha> Itens, string? ProximoCursor);
public sealed record EventoLinha(long Sequencia, DateTimeOffset OcorridoEm, Guid? AutorId, TipoEventoChamado Tipo);
public sealed record PaginaEventos(IReadOnlyList<EventoLinha> Itens, long? ProximaSequencia);

public interface IProntuarioConsultas
{
    Task<ChamadoDetalhe> ObterAsync(Guid id, CancellationToken ct = default);
    Task<PaginaMensagens> MensagensAsync(Guid id, string? cursor, int tamanho, CancellationToken ct = default);
    Task<PaginaEventos> EventosAsync(Guid id, long depois, int tamanho, CancellationToken ct = default);
}
