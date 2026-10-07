using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using v8desk.application.Integracoes;
using v8desk.infrastructure.Integracoes;
using v8desk.application.Abstractions;
using v8desk.application.Chamados;
using v8desk.application.Configuracao;
using v8desk.domain.Entities;
using v8desk.domain.Enums;
using v8desk.domain.Exceptions;
using v8desk.domain.ValueObjects;
using v8desk.infrastructure.Persistence;
using v8desk.infrastructure.Repositories;

namespace v8desk.domain.tests;

// Execução explícita em banco de testes. Cria schema isolado e não remove dados.
public static class PostgresIntegracaoTests
{
    public static async Task ExecutarAsync(string conexao)
    {
        var builder = new NpgsqlConnectionStringBuilder(conexao);
        if (builder.Database?.StartsWith("v8desk_test", StringComparison.Ordinal) != true)
            throw new InvalidOperationException("Use um banco exclusivo cujo nome comece com v8desk_test.");
        var schema = "v8desk_test_" + Guid.NewGuid().ToString("N");
        await using (var admin = new NpgsqlConnection(builder.ConnectionString))
        {
            await admin.OpenAsync();
            await using var criar = new NpgsqlCommand($"CREATE SCHEMA \"{schema}\"", admin);
            await criar.ExecuteNonQueryAsync();
        }
        builder.SearchPath = schema;
        Console.WriteLine($"Schema de integração criado: {schema}. Preservado para inspeção.");
        var empresa = new Empresa("Integração", new CalendarioEmpresa("UTC"));
        foreach (var dia in Enum.GetValues<DayOfWeek>())
            empresa.Calendario.DefinirExpediente(dia, [new(new(8, 0), new(17, 0))]);
        var setor = empresa.CriarSetor("TI");
        var usuario = new Usuario(empresa.Id, "Atendente");
        var colega = new Usuario(empresa.Id, "Colega");
        var terceiro = new Usuario(empresa.Id, "Sem vínculo");
        var vinculo = new VinculoSetor(usuario, setor.Id, PapelSetor.Atendente);
        var vinculoColega = new VinculoSetor(colega, setor.Id, PapelSetor.Solicitante);
        setor.FilaGeral.AdicionarMembro(vinculo);
        foreach (var prioridade in Enum.GetValues<Prioridade>())
            foreach (var tipo in Enum.GetValues<TipoSla>()) setor.Sla.DefinirMeta(prioridade, tipo, new(8));
        var categoria = setor.CriarCategoria("Sistema");
        var subcategoria = setor.CriarCategoria("Licenças", categoria);
        var agora = DateTimeOffset.UtcNow;
        var chamado = Chamado.Abrir(new(empresa.Id, usuario.Id, new(setor.Id, setor.Nome), setor.FilaGeral,
            subcategoria, "Título", "Descrição", Prioridade.Media, Visibilidade.CompartilhadoComSetor),
            new(usuario.Id, agora), new(new(setor.Id, setor.Nome), setor.Sla, empresa.PadraoCicloVida, empresa.Calendario));
        var options = new DbContextOptionsBuilder<V8DeskDbContext>().UseNpgsql(builder.ConnectionString,
            pg => pg.EnableRetryOnFailure().MigrationsHistoryTable("__ef_migrations_history", schema))
            .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking).Options;
        V8DeskDbContext Contexto(Guid? tenant = null) => new(options, new EmpresaTeste(tenant ?? empresa.Id));
        await using (var db = Contexto())
        {
            await db.Database.MigrateAsync();
            db.AddRange(empresa, setor, usuario, colega, terceiro, vinculo, vinculoColega, chamado);
            await db.SaveChangesAsync();
        }
        await using (var db = Contexto())
        {
            var carregado = (await new ChamadoRepository(db).ObterParaAtualizacaoAsync(chamado.Id))!;
            Assert.Equal(chamado.Eventos.Count, carregado.Eventos.Count);
            Assert.Equal(chamado.CicloAtual.CiclosSla.Count, carregado.CicloAtual.CiclosSla.Count);
            Assert.Equal(chamado.CategoriaAtual.Caminho, carregado.CategoriaAtual.Caminho);
            Assert.Equal(empresa.Calendario.Id, carregado.MetasSla.Calendario.Id);
            carregado.ResponderSolicitante("Primeira mensagem", new(usuario.Id, agora.AddMinutes(1)));
            await db.SaveChangesAsync();
        }
        await using (var db = Contexto(Guid.NewGuid())) Assert.False(await db.Chamados.AnyAsync());
        await using (var db = Contexto())
        {
            var carregado = (await new ConfiguracaoRepository(db).ObterSetorAsync(setor.Id))!;
            Assert.Equal(2, carregado.Categorias.Count);
            Assert.Equal(categoria.Id, carregado.Categorias.Single(x => x.Id == subcategoria.Id).Pai!.Id);
            Assert.True(carregado.FilaGeral.PodeAtender(usuario.Id));
            Assert.Equal(8m, carregado.Sla.ObterMeta(Prioridade.Media, TipoSla.PrimeiraResposta).Prazo.Valor);
        }
        await using (var db = Contexto())
        {
            var pagina = await new ChamadoConsultas(db, new Identidade(colega.Id)).ListarFilaAsync(setor.FilaGeralId);
            Assert.Equal(1, pagina.Count);
            var invisivel = await new ChamadoConsultas(db, new Identidade(terceiro.Id)).ListarFilaAsync(setor.FilaGeralId);
            Assert.Equal(0, invisivel.Count);
        }
        await using (var db = Contexto())
        {
            var carregado = (await new ChamadoRepository(db).ObterParaAtualizacaoAsync(chamado.Id))!;
            Assert.Equal(1, carregado.Mensagens.Count);
            Assert.Equal(2, carregado.Eventos.Count);
        }
        await using (var primeiro = Contexto())
        await using (var segundo = Contexto())
        {
            var a = (await new ChamadoRepository(primeiro).ObterParaAtualizacaoAsync(chamado.Id))!;
            var b = (await new ChamadoRepository(segundo).ObterParaAtualizacaoAsync(chamado.Id))!;
            a.ResponderSolicitante("Vencedor", new(usuario.Id, agora.AddMinutes(2)));
            b.ResponderSolicitante("Concorrente", new(usuario.Id, agora.AddMinutes(3)));
            await primeiro.SaveChangesAsync();
            await ExigirFalhaAsync<RegraNegocioException>(() => segundo.SaveChangesAsync());
        }
        await using (var db = Contexto())
        {
            var executor = new ExecutorComandoIdempotente(db, new Identidade(usuario.Id));
            var resultado = await executor.ExecutarAsync("chave-1", "responder:v1:teste", async ct =>
            {
                var agregado = (await new ChamadoRepository(db).ObterParaAtualizacaoAsync(chamado.Id, ct))!;
                agregado.ResponderSolicitante("Idempotente", new(usuario.Id, agora.AddMinutes(4)));
                return "resultado";
            });
            Assert.Equal("resultado", resultado);
        }
        await using (var db = Contexto())
        {
            var executor = new ExecutorComandoIdempotente(db, new Identidade(usuario.Id));
            Assert.Equal("resultado", await executor.ExecutarAsync("chave-1", "responder:v1:teste", _ => throw new Exception("Não deve repetir")));
            await ExigirFalhaAsync<RegraNegocioException>(() => executor.ExecutarAsync("chave-1", "outros-dados", _ => Task.FromResult("incorreto")));
        }
        var publicador = new PublicadorTeste();
        await using (var db = Contexto())
            Assert.Equal(4, await new OutboxProcessador(db, publicador, NullLogger<OutboxProcessador>.Instance).ProcessarAsync());
        await using (var db = Contexto())
        {
            Assert.Equal(4, await db.Set<OutboxMensagem>().CountAsync(x => x.ProcessadaEm != null));
            Assert.Equal(0, await new OutboxProcessador(db, publicador, NullLogger<OutboxProcessador>.Instance).ProcessarAsync());
            Assert.Equal(3, await db.Mensagens.CountAsync()); // A escrita concorrente foi revertida.
        }
        await using (var db = Contexto())
        {
            var agregado = (await new ChamadoRepository(db).ObterParaAtualizacaoAsync(chamado.Id))!;
            agregado.ResponderSolicitante("Publicação com falha", new(usuario.Id, agora.AddMinutes(5)));
            await db.SaveChangesAsync();
        }
        await using (var db = Contexto())
            Assert.Equal(0, await new OutboxProcessador(db, new PublicadorFalha(), NullLogger<OutboxProcessador>.Instance).ProcessarAsync(1));
        await using (var db = Contexto())
        {
            var pendente = await db.Set<OutboxMensagem>().AsTracking().SingleAsync(x => x.ProcessadaEm == null);
            Assert.Equal(1, pendente.Tentativas);
            Assert.True(pendente.DisponivelEm > pendente.CriadaEm);
            Assert.False(pendente.UltimoErro!.Contains("segredo"));
            // Antecipação apenas no schema de testes, sem esperar o relógio.
            db.Entry(pendente).Property(x => x.DisponivelEm).CurrentValue = DateTimeOffset.UtcNow.AddMinutes(-1);
            await db.SaveChangesAsync();
        }
        await using (var db = Contexto())
            Assert.Equal(1, await new OutboxProcessador(db, publicador, NullLogger<OutboxProcessador>.Instance).ProcessarAsync(1));
        ChamadoAplicacao Aplicacao(V8DeskDbContext db, Guid autor) => new(new ChamadoRepository(db), new ConfiguracaoRepository(db),
            new AcessoRepository(db), new ExecutorComandoIdempotente(db, new Identidade(autor)), db,
            new EmpresaTeste(empresa.Id), new Identidade(autor), TimeProvider.System);
        Guid chamadoAplicacao;
        await using (var db = Contexto())
        {
            var resultado = await Aplicacao(db, colega.Id).AbrirAsync(new(setor.Id, setor.Id, subcategoria.Id,
                "Via Application", "Pedido", Prioridade.Media, Visibilidade.CompartilhadoComSetor), "app-abrir");
            Assert.True(resultado.Numero > 0);
            chamadoAplicacao = resultado.Id;
        }
        await using (var db = Contexto())
        {
            var resultado = await Aplicacao(db, colega.Id).AbrirAsync(new(setor.Id, setor.Id, subcategoria.Id,
                "Via Application", "Pedido", Prioridade.Media, Visibilidade.CompartilhadoComSetor), "app-abrir");
            Assert.Equal(chamadoAplicacao, resultado.Id);
        }
        await using (var db = Contexto())
            Assert.Equal(StatusChamado.Aceito, (await Aplicacao(db, usuario.Id).ExecutarAsync(new AssumirChamado(chamadoAplicacao), "app-assumir")).Status);
        await using (var db = Contexto())
            Assert.Equal(StatusChamado.AguardandoInformacao, (await Aplicacao(db, usuario.Id).ExecutarAsync(new SolicitarInformacao(chamadoAplicacao, "Detalhes?"), "app-perguntar")).Status);
        await using (var db = Contexto())
            Assert.Equal(StatusChamado.Aceito, (await Aplicacao(db, colega.Id).ExecutarAsync(new ResponderSolicitante(chamadoAplicacao, "Mais detalhes"), "app-responder")).Status);
        await using (var db = Contexto())
        {
            var existente = await db.Vinculos.AsTracking().SingleAsync(x => x.Id == vinculo.Id);
            existente.ConcederPapel(PapelSetor.Gestor);
            await db.SaveChangesAsync();
        }
        await using (var db = Contexto())
        {
            var configuracao = new ConfiguracaoSetorAplicacao(new ConfiguracaoRepository(db), new AcessoRepository(db),
                new ExecutorComandoIdempotente(db, new Identidade(usuario.Id)), db, new EmpresaTeste(empresa.Id), new Identidade(usuario.Id), TimeProvider.System);
            var resultado = await configuracao.ExecutarAsync(new CriarFila(setor.Id, "Via Application"), "app-fila");
            Assert.True(await db.Filas.AnyAsync(f => f.Id == resultado.RegistroId));
        }
        await using (var db = Contexto())
            await Aplicacao(db, usuario.Id).ExecutarAsync(new AdicionarNotaInterna(chamadoAplicacao, "Nota privada"), "app-nota");
        await using (var db = Contexto())
        {
            var leitura = new ProntuarioConsultas(db, new AcessoRepository(db), new Identidade(colega.Id));
            Assert.Equal(chamadoAplicacao, (await leitura.ObterAsync(chamadoAplicacao)).Id);
            var mensagens = await leitura.MensagensAsync(chamadoAplicacao, null, 1);
            Assert.True(mensagens.ProximoCursor is not null);
            Assert.True(mensagens.Itens.All(m => m.Tipo == TipoMensagem.Publica));
            Assert.True((await leitura.MensagensAsync(chamadoAplicacao, mensagens.ProximoCursor, 100)).Itens.All(m => m.Tipo == TipoMensagem.Publica));
            Assert.True((await leitura.EventosAsync(chamadoAplicacao, 0, 100)).Itens.All(e => e.Tipo != TipoEventoChamado.NotaInternaAdicionada));
        }
        await using (var db = Contexto())
        {
            var leitura = new ProntuarioConsultas(db, new AcessoRepository(db), new Identidade(usuario.Id));
            Assert.True((await leitura.MensagensAsync(chamadoAplicacao, null, 100)).Itens.Any(m => m.Tipo == TipoMensagem.NotaInterna));
        }
        await VerificarIntegracoesAsync(builder.ConnectionString, empresa.Id, usuario.Id);
        Console.WriteLine("PASSOU: PostgreSQL real, migrations, round-trip, isolamento, concorrência, rollback, idempotência, outbox, Application, prontuário e integrações.");
    }

    private static async Task VerificarIntegracoesAsync(string conexao, Guid empresa, Guid usuario)
    {
        // O envio externo nunca fica dentro de uma estratégia de repetição automática do EF.
        var options = new DbContextOptionsBuilder<V8DeskDbContext>().UseNpgsql(conexao)
            .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking).Options;
        V8DeskDbContext Contexto(Guid? tenant = null) => new(options, new EmpresaTeste(tenant ?? empresa));
        var config = new IntegracoesEmpresa(empresa);
        config.ConfigurarEmail(new ConfiguracaoEmail
        {
            Provedor = ProvedorEmail.Smtp,
            Remetente = "v8desk@example.com",
            NomeRemetente = "V8Desk",
            HostSmtp = "smtp.example.com",
            PortaSmtp = 587,
            SegurancaSmtp = SegurancaSmtp.StartTls
        });
        var tenantMicrosoft = Guid.NewGuid(); var api = Guid.NewGuid(); var login = Guid.NewGuid(); var objeto = Guid.NewGuid();
        config.ConfigurarMicrosoft(new(tenantMicrosoft, api, login));
        config.AtivarMicrosoft(true);
        var entrega = Guid.CreateVersion7();
        await using (var db = Contexto())
        {
            var repo = new IntegracoesRepository(db);
            repo.Adicionar(config);
            await repo.ContatoAsync(usuario, "atendente@example.com", default);
            await repo.VincularAsync(usuario, tenantMicrosoft, objeto, true, true, default);
            await repo.RegistrarTesteAsync(entrega, usuario, config.RevisaoEmail, default);
            repo.Auditar(usuario, "ConfigurarEmail", DateTimeOffset.UtcNow);
            await db.SaveChangesAsync();
        }
        await using (var db = Contexto(Guid.NewGuid()))
        {
            Assert.Equal(0, await db.Set<IntegracoesEmpresa>().CountAsync());
            Assert.Equal(0, await db.Set<EntregaEmail>().CountAsync());
            Assert.Equal(0, await db.Set<VinculoMicrosoft>().CountAsync());
            Assert.Equal(0, await db.Set<ContatoNotificacao>().CountAsync());
            Assert.Equal(0, await db.Set<AuditoriaIntegracao>().CountAsync());
        }
        await using (var source = NpgsqlDataSource.Create(conexao))
        {
            var identidade = new IdentidadeMicrosoftRepository(source);
            var encontrada = await identidade.ResolverAsync(tenantMicrosoft, api, objeto, default);
            Assert.True(encontrada is { Ativa: true, Administrador: true });
            Assert.Equal(usuario, encontrada!.UsuarioId);
            Assert.Equal(login, (await identidade.AcessoAsync(empresa, default))!.ClienteLoginId);
            Assert.True(await identidade.ResolverAsync(tenantMicrosoft, Guid.NewGuid(), objeto, default) is null);
            Assert.True(await identidade.ResolverAsync(Guid.NewGuid(), api, objeto, default) is null);
        }
        var transporte = new TransporteIntegracaoTeste();
        await using (var db = Contexto())
            Assert.Equal(1, await new ProcessadorEmail(db, new IntegracoesRepository(db), new AcessoRepository(db),
                transporte, TimeProvider.System).ProcessarAsync());
        await using (var db = Contexto())
        {
            Assert.Equal("AceitaPeloProvedor", (await db.Set<EntregaEmail>().SingleAsync(e => e.Id == entrega)).Situacao);
            var salvo = (await new IntegracoesRepository(db).ObterAsync(true, default))!;
            salvo.AtivarEmail(true, DateTimeOffset.UtcNow);
            await db.SaveChangesAsync();
        }
        Assert.Equal(1, transporte.Envios);
        await using (var db = Contexto())
            Assert.Equal(0, await new ProcessadorEmail(db, new IntegracoesRepository(db), new AcessoRepository(db),
                transporte, TimeProvider.System).ProcessarAsync());
        // A entrega já aceita não é enviada de novo na próxima execução do worker.
        Assert.Equal(1, transporte.Envios);
        var falha = Guid.CreateVersion7();
        await using (var db = Contexto())
        {
            await new IntegracoesRepository(db).RegistrarTesteAsync(falha, usuario, config.RevisaoEmail, default);
            await db.SaveChangesAsync();
        }
        transporte.Falhar = true;
        await using (var db = Contexto())
            Assert.Equal(0, await new ProcessadorEmail(db, new IntegracoesRepository(db), new AcessoRepository(db),
                transporte, TimeProvider.System).ProcessarAsync());
        await using (var db = Contexto())
        {
            var pendente = await db.Set<EntregaEmail>().SingleAsync(e => e.Id == falha);
            Assert.Equal("Pendente", pendente.Situacao);
            Assert.Equal(1, pendente.Tentativas);
            Assert.Equal("limite_provedor", pendente.Codigo);
            Assert.True(pendente.ProximaTentativaEm > DateTimeOffset.UtcNow.AddSeconds(40));
        }
    }

    private sealed class TransporteIntegracaoTeste : ITransporteEmail
    {
        public int Envios { get; private set; }
        public bool Falhar { get; set; }
        public Task<DiagnosticoIntegracao> DiagnosticarAsync(ConfiguracaoEmail config, Guid empresa, CancellationToken ct) =>
            Task.FromResult(new DiagnosticoIntegracao(true, "conectado", "Conectado."));
        public Task EnviarAsync(ConfiguracaoEmail config, Guid empresa, EmailSaida email, CancellationToken ct)
        {
            if (Falhar) throw new FalhaIntegracaoException("limite_provedor", "Espere antes de tentar novamente.", true, TimeSpan.FromSeconds(55));
            Envios++; return Task.CompletedTask;
        }
    }

    private static async Task ExigirFalhaAsync<T>(Func<Task> executar) where T : Exception
    {
        try { await executar(); } catch (T) { return; }
        throw new Exception($"Esperada exceção {typeof(T).Name}.");
    }
    private sealed record EmpresaTeste(Guid EmpresaId) : IEmpresaAtual;
    private sealed record Identidade(Guid UsuarioId) : IUsuarioAtual;
    private sealed class PublicadorTeste : IPublicadorOutbox
    {
        public Task PublicarAsync(EntregaOutbox mensagem, CancellationToken cancellationToken) => Task.CompletedTask;
    }
    private sealed class PublicadorFalha : IPublicadorOutbox
    {
        public Task PublicarAsync(EntregaOutbox mensagem, CancellationToken cancellationToken) => throw new Exception("segredo do publicador");
    }
}
