using System.ComponentModel.DataAnnotations;
using v8desk.application.Chamados;
using v8desk.domain.Enums;

namespace v8desk.api.Contracts;

public sealed class PesquisaChamadosEntrada : IValidatableObject
{
    [IdValido] public Guid FilaId { get; init; }
    [EnumDataType(typeof(SituacaoListagem))] public SituacaoListagem Situacao { get; init; } = SituacaoListagem.EmAberto;
    [MaxLength(3)] public Prioridade[]? Prioridades { get; init; }
    [EnumDataType(typeof(FiltroResponsavel))] public FiltroResponsavel Responsavel { get; init; }
    [IdValido] public Guid? ResponsavelId { get; init; }
    [IdValido] public Guid? CategoriaId { get; init; }
    public DateTimeOffset? AbertoDesde { get; init; }
    public DateTimeOffset? AbertoAte { get; init; }
    [StringLength(200)] public string? Texto { get; init; }
    [EnumDataType(typeof(OrdemChamados))] public OrdemChamados Ordem { get; init; }
    [StringLength(4096)] public string? Cursor { get; init; }
    [Range(1, 100, ErrorMessage = "Escolha de 1 a 100 chamados por página.")] public int Tamanho { get; init; } = 25;
    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (Prioridades?.Any(p => !Enum.IsDefined(p)) == true)
            yield return new("Escolha prioridades válidas.", [nameof(Prioridades)]);
        if (AbertoDesde >= AbertoAte)
            yield return new("A data final deve ser posterior à data inicial.", [nameof(AbertoAte)]);
        if (Responsavel == FiltroResponsavel.Especifico && ResponsavelId is null)
            yield return new("Escolha o responsável para filtrar.", [nameof(ResponsavelId)]);
    }
    public FiltroChamados ParaFiltro() => new(FilaId)
    {
        Situacao = Situacao, Prioridades = Prioridades, Responsavel = Responsavel, ResponsavelId = ResponsavelId,
        CategoriaId = CategoriaId, AbertoDesde = AbertoDesde, AbertoAte = AbertoAte, Texto = Texto, Ordem = Ordem
    };
}
