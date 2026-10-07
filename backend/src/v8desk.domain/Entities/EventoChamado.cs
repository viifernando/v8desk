using System.Collections.Immutable;
namespace v8desk.domain.Entities;

public sealed record EventoChamado
{
    public Guid Id { get; }
    public Guid ChamadoId { get; }
    public DateTimeOffset OcorridoEm { get; }
    public Guid? AutorId { get; }
    public TipoEventoChamado Tipo { get; }
    public string? Motivo { get; }
    public long Sequencia { get; }
    public IReadOnlyDictionary<string, string?> Antes { get; }
    public IReadOnlyDictionary<string, string?> Depois { get; }

    public EventoChamado(Guid id, Guid chamadoId, DateTimeOffset ocorridoEm,
        Guid? autorId, TipoEventoChamado tipo, string? motivo,
        IReadOnlyDictionary<string, string?> antes, IReadOnlyDictionary<string, string?> depois, long sequencia = 0)
    {
        Id = id; ChamadoId = chamadoId; OcorridoEm = ocorridoEm;
        AutorId = autorId; Tipo = tipo; Motivo = motivo;
        Sequencia = sequencia;
        Antes = antes.ToImmutableDictionary(); Depois = depois.ToImmutableDictionary();
    }
}
