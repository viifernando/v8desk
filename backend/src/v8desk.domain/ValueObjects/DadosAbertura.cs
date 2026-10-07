namespace v8desk.domain.ValueObjects;

public sealed record DadosAbertura(
    Guid EmpresaId,
    Guid SolicitanteId,
    ReferenciaHistorica SetorOrigem,
    ReferenciaHistorica Fila,
    Categoria Categoria,
    string Titulo,
    string Descricao,
    Prioridade Prioridade,
    Visibilidade Visibilidade);
