using Microsoft.EntityFrameworkCore;
using v8desk.application.Chamados;
using v8desk.application.Abstractions;
using v8desk.domain.Enums;
using v8desk.domain.Exceptions;
using v8desk.domain.ValueObjects;
using v8desk.infrastructure.Persistence;
using v8desk.infrastructure.Repositories;

namespace v8desk.domain.tests;

public static class PaginacaoTests
{
    public static void TodasAsOrdensGeramSqlComCursorEFiltros()
    {
        using var db = new V8DeskDbContext(new DbContextOptionsBuilder<V8DeskDbContext>()
            .UseNpgsql("Host=localhost;Database=modelo_apenas;Username=modelo;Password=nao_utilizada").Options,
            new EmpresaTeste(Guid.NewGuid()));
        var consultas = new ChamadoConsultas(db, new UsuarioTeste(Guid.NewGuid()));
        var filtro = new FiltroChamados(Guid.NewGuid())
        {
            Prioridades = [Prioridade.Urgente, Prioridade.Media],
            Responsavel = FiltroResponsavel.Eu,
            CategoriaId = Guid.NewGuid(),
            AbertoDesde = DateTimeOffset.UtcNow.AddDays(-30),
            AbertoAte = DateTimeOffset.UtcNow,
            Texto = "100%_impressora"
        };
        foreach (var ordem in Enum.GetValues<OrdemChamados>())
        {
            var filtrada = consultas.Filtrar(db.Chamados, filtro with { Ordem = ordem });
            var primeira = consultas.Paginar(filtrada, ordem, null, 25).ToQueryString();
            Assert.True(primeira.Contains("LIMIT"));
            Assert.True(primeira.Contains("ILIKE"));
            var cursor = ChamadoConsultas.EscreverCursor(ordem, Linha(Prioridade.Media, DateTimeOffset.UtcNow));
            var marcador = ChamadoConsultas.LerCursor(cursor, ordem);
            Assert.True(consultas.Paginar(filtrada, ordem, marcador, 25).ToQueryString().Length > primeira.Length);
        }
        var semPrazo = ChamadoConsultas.EscreverCursor(OrdemChamados.PrazoMaisProximo, Linha(Prioridade.Baixa, null));
        Assert.Equal<DateTimeOffset?>(null, ChamadoConsultas.LerCursor(semPrazo, OrdemChamados.PrazoMaisProximo).Data);
        Assert.True(consultas.Filtrar(db.Chamados, filtro with { Texto = "#1048" }).ToQueryString().Contains("numero"));
        Assert.True(consultas.Filtrar(db.Chamados, new FiltroChamados(Guid.NewGuid()) { Situacao = SituacaoListagem.Encerrados })
            .ToQueryString().Contains("Encerrado"));
    }

    public static void CursorAdulteradoOuDeOutraOrdemEhRecusado()
    {
        var cursor = ChamadoConsultas.EscreverCursor(OrdemChamados.MaisRecentes, Linha(Prioridade.Media, DateTimeOffset.UtcNow));
        Assert.Equal(OrdemChamados.MaisRecentes, ChamadoConsultas.LerCursor(cursor, OrdemChamados.MaisRecentes).Ordem);
        foreach (var invalido in new[] { "lixo", cursor + "x", "", "eyJPcmRlbSI6OTl9" })
        {
            var erro = Assert.ThrowsReturning<ValidacaoDominioException>(() => ChamadoConsultas.LerCursor(invalido, OrdemChamados.MaisRecentes));
            Assert.Equal("cursor_invalido", erro.Codigo);
        }
        Assert.ThrowsReturning<ValidacaoDominioException>(() => ChamadoConsultas.LerCursor(cursor, OrdemChamados.Prioridade));
    }

    private static ChamadoLinha Linha(Prioridade prioridade, DateTimeOffset? vencimento)
    {
        var agora = DateTimeOffset.UtcNow;
        var setor = new ReferenciaHistorica(Guid.NewGuid(), "TI");
        return new ChamadoLinha(Guid.NewGuid(), 1048, "Impressora", StatusChamado.Aceito, prioridade,
            Visibilidade.CompartilhadoComSetor, agora, agora, vencimento, "Carlos",
            new CaminhoCategoria([new ReferenciaHistorica(Guid.NewGuid(), "Equipamentos")]),
            new ContextoAtendimento(setor, new ReferenciaHistorica(Guid.NewGuid(), "Geral"), null));
    }

    private sealed record EmpresaTeste(Guid EmpresaId) : IEmpresaAtual;
    private sealed record UsuarioTeste(Guid UsuarioId) : IUsuarioAtual;
}
