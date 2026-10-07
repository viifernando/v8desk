namespace v8desk.domain.ValueObjects;

public sealed record DadosAbertura(
    Guid EmpresaId,
    Guid SolicitanteId,
    ReferenciaHistorica SetorOrigem,
    ReferenciaHistorica Fila,
    Guid CategoriaId,
    string Titulo,
    string Descricao,
    Prioridade Prioridade,
    Visibilidade Visibilidade);
