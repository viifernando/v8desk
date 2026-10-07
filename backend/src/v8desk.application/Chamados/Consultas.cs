using v8desk.domain.Enums;
using v8desk.domain.ValueObjects;

namespace v8desk.application.Chamados;

public sealed record ChamadoResumo(Guid Id, string Titulo, StatusChamado Status, Prioridade Prioridade,
    DateTimeOffset AbertoEm, Guid? ResponsavelId);
public sealed record CursorChamados(DateTimeOffset AbertoEm, Guid Id);
public enum SituacaoListagem { EmAberto, AguardandoValidacao, Encerrados, Todos }
public enum OrdemChamados { PrazoMaisProximo, MaisRecentes, MaisAntigos, AtualizadosRecentemente, Prioridade }
public enum FiltroResponsavel { Qualquer, Eu, SemResponsavel, Especifico }
public sealed record FiltroChamados(Guid FilaId)
{
    public SituacaoListagem Situacao { get; init; } = SituacaoListagem.EmAberto;
    public IReadOnlyCollection<Prioridade>? Prioridades { get; init; }
    public FiltroResponsavel Responsavel { get; init; } = FiltroResponsavel.Qualquer;
    public Guid? ResponsavelId { get; init; }
    public Guid? CategoriaId { get; init; }
    public DateTimeOffset? AbertoDesde { get; init; }
    public DateTimeOffset? AbertoAte { get; init; }
    public string? Texto { get; init; }
    public OrdemChamados Ordem { get; init; } = OrdemChamados.PrazoMaisProximo;
}
public sealed record ChamadoLinha(Guid Id, long Numero, string Titulo, StatusChamado Status, Prioridade Prioridade,
    Visibilidade Visibilidade, DateTimeOffset AbertoEm, DateTimeOffset AtualizadoEm, DateTimeOffset? ProximoVencimentoEm,
    string Solicitante, CaminhoCategoria Categoria, ContextoAtendimento Contexto);
public sealed record PaginaChamados(IReadOnlyList<ChamadoLinha> Itens, string? ProximoCursor, int Total, bool TotalExcedeLimite);
public interface IChamadoConsultas
{
    Task<PaginaChamados> PesquisarAsync(FiltroChamados filtro, string? cursor = null, int tamanho = 25, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ChamadoResumo>> ListarFilaAsync(Guid filaId, CursorChamados? depois = null,
        int tamanho = 50, CancellationToken cancellationToken = default, Guid? categoriaId = null);
}
