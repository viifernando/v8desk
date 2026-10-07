using Microsoft.EntityFrameworkCore;
using v8desk.application.Abstractions;
using v8desk.domain.Entities;
using v8desk.domain.Exceptions;
using v8desk.domain.Enums;
using v8desk.domain.ValueObjects;
using Microsoft.EntityFrameworkCore.Diagnostics;
using v8desk.infrastructure.Persistence;

namespace v8desk.domain.tests;

public static class PersistenciaTests
{
    public static void ModeloPostgresGeraSchemaComIndicesConcorrenciaEIsolamento()
    {
        using var db = Criar(Guid.NewGuid());
        var modelo = db.Model;
        Assert.True(modelo.GetEntityTypes().All(e => e.GetDeclaredQueryFilters().Any()));
        Assert.True(modelo.GetEntityTypes().All(e => e.FindProperty("xmin")!.IsConcurrencyToken));
        var sql = db.Database.GenerateCreateScript();
        Assert.True(sql.Contains("jsonb"));
        Assert.True(sql.Contains("GENERATED ALWAYS AS"));
        Assert.True(sql.Contains("PRIMARY KEY (empresa_id, id)"));
        Assert.True(sql.Contains("finalizado_em IS NULL"));
        Assert.True(sql.Contains("eventos_chamado"));
        Assert.True(sql.Contains("outbox"));
        Assert.False(sql.Contains("xmin xid")); // xmin é coluna de sistema, não deve ser criada.
    }

    public static void FiltroDeEmpresaEstaNoSqlETrocaPorContexto()
    {
        using var primeiro = Criar(Guid.NewGuid());
        using var segundo = Criar(Guid.NewGuid());
        var sql1 = primeiro.Chamados.ToQueryString();
        var sql2 = segundo.Chamados.ToQueryString();
        Assert.True(sql1.Contains(primeiro.EmpresaId.ToString()));
        Assert.True(sql2.Contains(segundo.EmpresaId.ToString()));
        Assert.False(sql2.Contains(primeiro.EmpresaId.ToString()));
        var instante = DateTimeOffset.UtcNow;
        var cursorId = Guid.NewGuid();
        var pagina = primeiro.Chamados.Where(x => x.AbertoEm > instante ||
            (x.AbertoEm == instante && x.Id.CompareTo(cursorId) > 0))
            .OrderBy(x => x.AbertoEm).ThenBy(x => x.Id).Take(50).ToQueryString();
        Assert.True(pagina.Contains("LIMIT"));
    }

    public static void GravacaoDeOutraEmpresaFalhaAntesDeAcessarBanco()
    {
        using var db = Criar(Guid.NewGuid());
        db.Usuarios.Add(new Usuario(Guid.NewGuid(), "Externo"));
        Assert.Throws<AcessoNegadoException>(() => db.SaveChanges());
        using var outro = Criar(Guid.NewGuid());
        var externa = new Empresa("Outra empresa", new("UTC"));
        outro.Setores.Add(externa.CriarSetor("TI"));
        Assert.Throws<AcessoNegadoException>(() => outro.SaveChanges());
    }

    public static void SnapshotsJsonPreservamCalendarioAnexosECaminho()
    {
        using var db = Criar(Guid.NewGuid());
        var calendario = new CalendarioEmpresa("UTC");
        calendario.DefinirExpediente(DayOfWeek.Monday, [new(new(8, 0), new(17, 0))]);
        calendario.RegistrarExcecao(new(new(2026, 10, 5), "Feriado", []));
        var modelo = db.Model.FindEntityType(typeof(PeriodoEtapa))!;
        var converter = modelo.FindProperty(nameof(PeriodoEtapa.CalendarioAplicado))!.GetValueConverter()!;
        var restaurado = (CalendarioEmpresa)converter.ConvertFromProvider(converter.ConvertToProvider(calendario.CriarSnapshot()))!;
        Assert.Equal(calendario.Id, restaurado.Id);
        Assert.Equal(calendario.Versao, restaurado.Versao);
        Assert.False(restaurado.PossuiExpediente(new(2026, 10, 5)));
        Assert.True(restaurado.PossuiExpediente(new(2026, 10, 12)));
        Assert.Throws<RegraNegocioException>(() => restaurado.RemoverExcecao(new(2026, 10, 5)));

        var anexos = new List<Anexo> { new("erro.png", "image/png", 123, "objeto/123", "hash", Guid.NewGuid(), DateTimeOffset.UtcNow) };
        converter = db.Model.FindEntityType(typeof(Mensagem))!.FindProperty("_anexos")!.GetValueConverter()!;
        var copia = (List<Anexo>)converter.ConvertFromProvider(converter.ConvertToProvider(anexos))!;
        Assert.Equal(anexos[0].Id, copia[0].Id);

        var caminho = new CaminhoCategoria([new(Guid.NewGuid(), "Sistemas"), new(Guid.NewGuid(), "ERP")]);
        converter = db.Model.FindEntityType(typeof(Chamado))!.FindProperty(nameof(Chamado.CategoriaAtual))!.GetValueConverter()!;
        var categoria = (CaminhoCategoria)converter.ConvertFromProvider(converter.ConvertToProvider(caminho))!;
        Assert.Equal(caminho.Id, categoria.Id);
        Assert.Equal("Sistemas → ERP", categoria.Caminho);
    }

    public static void GrafoDeChamadoRecebeEmpresaEOutboxSemDependenciaDeBanco()
    {
        var empresa = new Empresa("Empresa", new CalendarioEmpresa("UTC"));
        var setor = empresa.CriarSetor("TI");
        var usuario = new Usuario(empresa.Id, "Carlos");
        var categoria = setor.CriarCategoria("Sistemas");
        foreach (var prioridade in Enum.GetValues<Prioridade>())
            foreach (var tipo in Enum.GetValues<TipoSla>()) setor.Sla.DefinirMeta(prioridade, tipo, new(8));
        var instante = DateTimeOffset.UtcNow;
        var chamado = Chamado.Abrir(new(empresa.Id, usuario.Id, new(setor.Id, setor.Nome), setor.FilaGeral,
            categoria, "Título", "Descrição", Prioridade.Media, Visibilidade.CompartilhadoComSetor),
            new(usuario.Id, instante), new(new(setor.Id, setor.Nome), setor.Sla, empresa.PadraoCicloVida, empresa.Calendario));
        using var db = new V8DeskDbContext(new DbContextOptionsBuilder<V8DeskDbContext>()
            .UseNpgsql("Host=localhost;Database=sem_conexao;Username=teste")
            .AddInterceptors(new SemGravacao()).Options, new EmpresaTeste(empresa.Id));
        db.AddRange(empresa, setor, usuario, chamado);
        Assert.Equal(0, db.SaveChanges()); // Interceptor suprime o acesso ao banco, após validações locais.
        Assert.True(db.ChangeTracker.Entries().Where(e => e.Entity is not Empresa)
            .All(e => (Guid)e.Property("EmpresaId").CurrentValue! == empresa.Id));
        Assert.Equal(chamado.Eventos.Count, db.ChangeTracker.Entries<OutboxMensagem>().Count());
        var json = db.ChangeTracker.Entries<OutboxMensagem>().Single().Entity.Payload;
        Assert.True(json.Contains("Sequencia"));
    }

    private sealed class SemGravacao : SaveChangesInterceptor
    {
        public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
            => InterceptionResult<int>.SuppressWithResult(0);
    }

    private static V8DeskDbContext Criar(Guid empresaId)
        => new(new DbContextOptionsBuilder<V8DeskDbContext>()
            .UseNpgsql("Host=localhost;Database=modelo_apenas;Username=modelo;Password=nao_utilizada").Options,
            new EmpresaTeste(empresaId));

    private sealed record EmpresaTeste(Guid EmpresaId) : IEmpresaAtual;
}
