using System.ComponentModel.DataAnnotations;
using v8desk.domain.Enums;
using v8desk.domain.ValueObjects;

namespace v8desk.api.Contracts;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class IdValidoAttribute : ValidationAttribute
{
    public IdValidoAttribute() : base("Escolha uma opção válida.") { }
    public override bool IsValid(object? value) => value is null || value is Guid id && id != Guid.Empty;
}
public sealed record AbrirChamadoEntrada
{
    [IdValido] public required Guid SetorOrigemId { get; init; }
    [IdValido] public required Guid SetorResponsavelId { get; init; }
    [IdValido] public required Guid CategoriaId { get; init; }
    [Required(ErrorMessage = "Informe o título."), StringLength(200, ErrorMessage = "Use até 200 caracteres no título.")] public required string Titulo { get; init; }
    [Required(ErrorMessage = "Descreva sua solicitação."), StringLength(20000, ErrorMessage = "Use até 20.000 caracteres na descrição.")] public required string Descricao { get; init; }
    [EnumDataType(typeof(Prioridade), ErrorMessage = "Escolha uma prioridade válida.")] public required Prioridade Prioridade { get; init; }
    [EnumDataType(typeof(Visibilidade), ErrorMessage = "Escolha uma visibilidade válida.")] public required Visibilidade Visibilidade { get; init; }
}
public sealed record TextoEntrada
{
    [Required(ErrorMessage = "Escreva a mensagem."), StringLength(20000, ErrorMessage = "Use até 20.000 caracteres na mensagem.")] public required string Texto { get; init; }
}
public sealed record MotivoEntrada
{
    [Required(ErrorMessage = "Informe o motivo."), StringLength(2000, ErrorMessage = "Use até 2.000 caracteres no motivo.")] public required string Motivo { get; init; }
}
public sealed record ResponsavelEntrada
{
    [IdValido] public required Guid? UsuarioId { get; init; }
    [Required(ErrorMessage = "Informe o motivo."), StringLength(2000)] public required string Motivo { get; init; }
}
public sealed record PrioridadeEntrada
{
    [EnumDataType(typeof(Prioridade))] public required Prioridade Prioridade { get; init; }
    [Required(ErrorMessage = "Informe o motivo."), StringLength(2000)] public required string Motivo { get; init; }
}
public sealed record CategoriaEntrada
{
    [IdValido] public required Guid CategoriaId { get; init; }
    [Required(ErrorMessage = "Informe o motivo."), StringLength(2000)] public required string Motivo { get; init; }
}
public sealed record TransferenciaEntrada
{
    [IdValido] public required Guid SetorDestinoId { get; init; }
    [IdValido] public required Guid FilaDestinoId { get; init; }
    [IdValido] public required Guid CategoriaDestinoId { get; init; }
    [Required(ErrorMessage = "Informe o motivo."), StringLength(2000)] public required string Motivo { get; init; }
}
public sealed record VisibilidadeEntrada
{
    [EnumDataType(typeof(Visibilidade))] public required Visibilidade Visibilidade { get; init; }
    [Required(ErrorMessage = "Informe o motivo."), StringLength(2000)] public required string Motivo { get; init; }
}
public sealed record AvaliacaoEntrada
{
    [Range(1, 5, ErrorMessage = "Escolha uma nota de 1 a 5.")] public required int Nota { get; init; }
    [StringLength(2000)] public string? Comentario { get; init; }
}
public sealed record CorrecaoMensagemEntrada
{
    [Required(ErrorMessage = "Escreva a mensagem."), StringLength(20000)] public required string Texto { get; init; }
    [Required(ErrorMessage = "Informe o motivo da correção."), StringLength(2000)] public required string Motivo { get; init; }
}
public sealed record NomeEntrada
{
    [Required(ErrorMessage = "Informe o nome."), StringLength(200, ErrorMessage = "Use até 200 caracteres no nome.")] public required string Nome { get; init; }
}
public sealed record DisponibilidadeEntrada { public required bool Disponivel { get; init; } }
public sealed record NovaCategoriaEntrada
{
    [Required(ErrorMessage = "Informe o nome."), StringLength(200)] public required string Nome { get; init; }
    [IdValido] public Guid? PaiId { get; init; }
}
public sealed record PaiCategoriaEntrada { [IdValido] public required Guid? PaiId { get; init; } }
public sealed record DestinoCategoriaEntrada { [IdValido] public required Guid? FilaId { get; init; } }
public sealed record RecebimentoCategoriaEntrada { public required bool PermitirComSubcategorias { get; init; } }
public sealed record VisibilidadeCategoriaEntrada { [EnumDataType(typeof(Visibilidade))] public required Visibilidade Visibilidade { get; init; } }
public sealed record PapelEntrada { [EnumDataType(typeof(PapelSetor))] public required PapelSetor Papel { get; init; } }
public sealed record VinculoEntrada
{
    [IdValido] public required Guid UsuarioId { get; init; }
    [EnumDataType(typeof(PapelSetor))] public required PapelSetor Papel { get; init; }
}
public sealed record AcessoRestritoEntrada { [EnumDataType(typeof(AcessoRestritoFila))] public required AcessoRestritoFila Regra { get; init; } }
public sealed record MetaSlaEntrada
{
    [EnumDataType(typeof(Prioridade))] public required Prioridade Prioridade { get; init; }
    [EnumDataType(typeof(TipoSla))] public required TipoSla Tipo { get; init; }
    [Range(typeof(decimal), "0.0001", "100000", ErrorMessage = "Informe um prazo positivo em horas úteis, de até 100.000 horas.")] public required decimal HorasUteis { get; init; }
}
public sealed record CicloVidaEntrada
{
    [Range(typeof(decimal), "0.0001", "100000")] public required decimal HorasUteisValidacao { get; init; }
    [Range(1, 3650)] public required int DiasReabertura { get; init; }
    [Range(1, 3650)] public required int DiasAvaliacao { get; init; }
    [Range(0, 3650)] public required int DiasUteisAntecedenciaLembrete { get; init; }
    public PoliticaCicloVida ParaDominio() => new(new HorasUteis(HorasUteisValidacao),
        TimeSpan.FromDays(DiasReabertura), DiasUteisAntecedenciaLembrete, TimeSpan.FromDays(DiasAvaliacao));
}
public sealed record LimiteSetoresEntrada { [Range(1, 10000)] public required int? Limite { get; init; } }
public sealed record FusoEntrada { [Required(ErrorMessage = "Informe o fuso horário."), StringLength(200)] public required string FusoHorarioId { get; init; } }
public sealed record IntervaloEntrada { public required TimeOnly Inicio { get; init; } public required TimeOnly Fim { get; init; } }
public sealed record ExpedienteEntrada : IValidatableObject
{
    [EnumDataType(typeof(DayOfWeek))] public required DayOfWeek Dia { get; init; }
    [Required, MaxLength(24)] public required IntervaloEntrada[] Intervalos { get; init; }
    public IEnumerable<ValidationResult> Validate(ValidationContext context) => ValidacaoIntervalos.Validar(Intervalos);
}
public sealed record ExcecaoCalendarioEntrada : IValidatableObject
{
    public required DateOnly Data { get; init; }
    [Required(ErrorMessage = "Informe o motivo da exceção."), StringLength(2000)] public required string Motivo { get; init; }
    [Required, MaxLength(24)] public required IntervaloEntrada[] Intervalos { get; init; }
    public IEnumerable<ValidationResult> Validate(ValidationContext context) => ValidacaoIntervalos.Validar(Intervalos);
}
internal static class ValidacaoIntervalos
{
    internal static IEnumerable<ValidationResult> Validar(IntervaloEntrada[]? intervalos)
    {
        if (intervalos?.Any(i => i is null) == true)
            yield return new("Informe o início e o fim de cada horário.", ["Intervalos"]);
    }
}
