namespace v8desk.domain.Entities;

public sealed record EventoChamado(
    Guid Id,
    Guid ChamadoId,
    DateTimeOffset OcorridoEm,
    Guid? AutorId,
    TipoEventoChamado Tipo,
    string? Motivo,
    IReadOnlyDictionary<string, string?> Antes,
    IReadOnlyDictionary<string, string?> Depois);
