using v8desk.domain.Enums;

namespace v8desk.application.Chamados;

public sealed record AbrirChamado(Guid SetorOrigemId, Guid SetorResponsavelId, Guid CategoriaId,
    string Titulo, string Descricao, Prioridade Prioridade, Visibilidade Visibilidade);
public abstract record ComandoChamado(Guid ChamadoId);
public sealed record AceitarChamado(Guid ChamadoId) : ComandoChamado(ChamadoId);
public sealed record AssumirChamado(Guid ChamadoId) : ComandoChamado(ChamadoId);
public sealed record IniciarAtendimento(Guid ChamadoId) : ComandoChamado(ChamadoId);
public sealed record TrocarResponsavel(Guid ChamadoId, Guid? UsuarioId, string Motivo) : ComandoChamado(ChamadoId);
public sealed record SolicitarInformacao(Guid ChamadoId, string Pergunta) : ComandoChamado(ChamadoId);
public sealed record ResponderSolicitante(Guid ChamadoId, string Texto) : ComandoChamado(ChamadoId);
public sealed record ResponderEquipe(Guid ChamadoId, string Texto) : ComandoChamado(ChamadoId);
public sealed record AdicionarNotaInterna(Guid ChamadoId, string Texto) : ComandoChamado(ChamadoId);
public sealed record AlterarPrioridade(Guid ChamadoId, Prioridade Prioridade, string Motivo) : ComandoChamado(ChamadoId);
public sealed record AlterarCategoria(Guid ChamadoId, Guid CategoriaId, string Motivo) : ComandoChamado(ChamadoId);
public sealed record TransferirChamado(Guid ChamadoId, Guid SetorDestinoId, Guid FilaDestinoId,
    Guid CategoriaDestinoId, string Motivo) : ComandoChamado(ChamadoId);
public sealed record AlterarVisibilidade(Guid ChamadoId, Visibilidade Visibilidade, string Motivo) : ComandoChamado(ChamadoId);
public sealed record ResolverChamado(Guid ChamadoId, string Solucao) : ComandoChamado(ChamadoId);
public sealed record ConfirmarSolucao(Guid ChamadoId) : ComandoChamado(ChamadoId);
public sealed record RejeitarSolucao(Guid ChamadoId, string Motivo) : ComandoChamado(ChamadoId);
public sealed record ReabrirChamado(Guid ChamadoId, string Motivo) : ComandoChamado(ChamadoId);
public sealed record CancelarChamado(Guid ChamadoId, string Motivo) : ComandoChamado(ChamadoId);
public sealed record AvaliarChamado(Guid ChamadoId, int Nota, string? Comentario) : ComandoChamado(ChamadoId);
public sealed record CorrigirMensagem(Guid ChamadoId, Guid MensagemId, string Texto, string Motivo) : ComandoChamado(ChamadoId);
public sealed record ResultadoChamado(Guid Id, long Numero, StatusChamado Status, long Versao);
