using v8desk.application.Abstractions;
using v8desk.application.Chamados;
using v8desk.application.Configuracao;
using v8desk.application.Seguranca;
using v8desk.domain.Entities;
using v8desk.domain.Enums;
using v8desk.domain.Exceptions;
using Microsoft.Extensions.DependencyInjection;

namespace v8desk.domain.tests;

public static class AplicacaoTests
{
    public static void ComposicaoResolveCasosDeUsoSemAbrirConexao()
    {
        var f = new Cenario();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IEmpresaAtual>(f);
        services.AddSingleton<IUsuarioAtual>(f);
        services.AddSingleton<IAutorizacaoEmpresa>(f);
        v8desk.application.DependencyInjection.AdicionarAplicacao(services);
        v8desk.infrastructure.DependencyInjection.AdicionarInfraestrutura(services, "Host=localhost;Database=sem_conexao;Username=teste");
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        using var scope = provider.CreateScope();
        Assert.True(scope.ServiceProvider.GetRequiredService<ChamadoAplicacao>() is not null);
        Assert.True(scope.ServiceProvider.GetRequiredService<ConfiguracaoSetorAplicacao>() is not null);
        Assert.True(scope.ServiceProvider.GetRequiredService<ConfiguracaoEmpresaAplicacao>() is not null);
        Assert.True(scope.ServiceProvider.GetRequiredService<IChamadoConsultas>() is not null);
    }

    public static void DisponibilidadePessoalNaoExigeAdministracaoENaoAceitaUsuarioInativo()
    {
        var f = new Cenario();
        var perfil = new v8desk.application.Usuarios.UsuarioAplicacao(f, f, f, f, f, f);
        var comando = new v8desk.application.Usuarios.DefinirMinhaDisponibilidade(false);
        var resultado = perfil.DefinirDisponibilidadeAsync(comando, "disponivel").GetAwaiter().GetResult();
        Assert.Equal(f.Solicitante.Id, resultado.UsuarioId);
        Assert.False(f.Solicitante.DisponivelParaAtendimento);
        Assert.True(f.Atendente.DisponivelParaAtendimento);
        f.Solicitante.Desativar();
        Assert.Throws<AcessoNegadoException>(() => perfil.DefinirDisponibilidadeAsync(comando, "disponivel").GetAwaiter().GetResult());
        Assert.Equal(1, f.Salvamentos);
    }
    public static void AdministracaoConfiguraEmpresaSemReescreverCalendarioDoChamado()
    {
        var f = new Cenario();
        Assert.Throws<AcessoNegadoException>(() => f.ConfiguracaoEmpresa.ExecutarAsync(new CriarSetor("RH"), "rh").GetAwaiter().GetResult());
        var id = f.Aplicacao.AbrirAsync(f.Abertura(), "abrir").GetAwaiter().GetResult().Id;
        var versao = f.Empresa.Calendario.Versao;
        f.Administrador = true;
        var novo = f.ConfiguracaoEmpresa.ExecutarAsync(new CriarSetor("RH"), "rh").GetAwaiter().GetResult();
        Assert.True(f.Vinculos.Any(v => v.SetorId == novo.RegistroId && v.UsuarioId == f.Autor.Id && v.PossuiPapel(PapelSetor.Gestor)));
        f.ConfiguracaoEmpresa.ExecutarAsync(new DefinirExpediente(DayOfWeek.Wednesday, [new(new(9, 0), new(18, 0))]), "expediente").GetAwaiter().GetResult();
        Assert.True(f.Empresa.Calendario.Versao > versao);
        Assert.Equal(versao, f.Chamados[id].MetasSla.Calendario.Versao);
        f.Administrador = false;
        Assert.Throws<AcessoNegadoException>(() => f.ConfiguracaoEmpresa.ExecutarAsync(new CriarSetor("RH"), "rh").GetAwaiter().GetResult());
    }

    public static void LimiteDeSetoresEUnicidadeDeNomesContinuamNoDominio()
    {
        var f = new Cenario();
        f.Empresa.ConfigurarLimiteAtuacao(1);
        f.Autor = f.Solicitante;
        f.Vinculos.Single(v => v.UsuarioId == f.Solicitante.Id).ConcederPapel(PapelSetor.Gestor);
        Assert.Throws<RegraNegocioException>(() => f.Configuracao.ExecutarAsync(new CriarVinculoSetor(f.Origem.Id, f.Atendente.Id, PapelSetor.Atendente), "limite").GetAwaiter().GetResult());
        f.Autor = f.Atendente;
        f.VinculoAtendente.ConcederPapel(PapelSetor.Gestor);
        var outra = f.Atendimento.CriarCategoria("Equipamentos");
        Assert.Throws<RegraNegocioException>(() => f.Configuracao.ExecutarAsync(new RenomearCategoria(f.Atendimento.Id, outra.Id, "licenças"), "duplicado").GetAwaiter().GetResult());
        Assert.Equal("Equipamentos", outra.Nome);
        Assert.Equal(0, f.Salvamentos);
    }

    public static void EncerramentoAutomaticoNaoRepeteEventos()
    {
        var f = new Cenario();
        var id = f.Aplicacao.AbrirAsync(f.Abertura(), "abrir").GetAwaiter().GetResult().Id;
        f.Autor = f.Atendente;
        f.Executar(new ResolverChamado(id, "Pronto"));
        var auto = new EncerramentoAutomaticoAplicacao(f, f, f, f.Relogio);
        Assert.False(auto.EncerrarAsync(id).GetAwaiter().GetResult());
        f.Relogio.Agora = f.Chamados[id].LimiteValidacao!.Value;
        Assert.True(auto.EncerrarAsync(id).GetAwaiter().GetResult());
        var eventos = f.Chamados[id].Eventos.Count;
        Assert.False(auto.EncerrarAsync(id).GetAwaiter().GetResult());
        Assert.Equal(eventos, f.Chamados[id].Eventos.Count);
        Assert.True(f.Chamados[id].Eventos.Last().AutorId is null);
    }
    public static void FluxoCompletoEscolheFilaDaCategoriaERetornaResponsavelNaReabertura()
    {
        var f = new Cenario();
        var fila = f.Atendimento.CriarFila("Licenciamento");
        fila.AdicionarMembro(f.VinculoAtendente);
        f.Categoria.ConfigurarDestino(fila);
        var aberto = f.Aplicacao.AbrirAsync(f.Abertura(), "abrir").GetAwaiter().GetResult();
        var chamado = f.Chamados[aberto.Id];
        Assert.Equal(fila.Id, chamado.FilaAtualId);
        Assert.Equal(f.Origem.Id, chamado.SetorOrigemNaAbertura.Id);
        f.Autor = f.Atendente;
        f.Executar(new AssumirChamado(chamado.Id));
        f.Executar(new SolicitarInformacao(chamado.Id, "Qual licença?"));
        Assert.Equal(StatusChamado.AguardandoInformacao, chamado.Status);
        f.Autor = f.Solicitante;
        f.Executar(new ResponderSolicitante(chamado.Id, "Licença anual"));
        Assert.Equal(StatusChamado.Aceito, chamado.Status);
        f.Autor = f.Atendente;
        f.Executar(new ResolverChamado(chamado.Id, "Licença instalada"));
        f.Autor = f.Solicitante;
        f.Executar(new ConfirmarSolucao(chamado.Id));
        f.Executar(new AvaliarChamado(chamado.Id, 5, null));
        f.Executar(new ReabrirChamado(chamado.Id, "Não funciona"));
        Assert.Equal(StatusChamado.Aceito, chamado.Status);
        Assert.Equal(2, chamado.CicloAtual.Numero);
        Assert.Equal(f.Atendente.Id, chamado.ResponsavelId);
        Assert.Equal(8, f.Salvamentos);
    }

    public static void ReplayNaoRepeteAcaoERevalidaPermissoes()
    {
        var f = new Cenario();
        var id = f.Aplicacao.AbrirAsync(f.Abertura(), "abrir").GetAwaiter().GetResult().Id;
        f.Autor = f.Atendente;
        var comando = new ResponderEquipe(id, "Retorno");
        var primeiro = f.Aplicacao.ExecutarAsync(comando, "responder").GetAwaiter().GetResult();
        var repetido = f.Aplicacao.ExecutarAsync(comando, "responder").GetAwaiter().GetResult();
        Assert.Equal(primeiro, repetido);
        Assert.Equal(1, f.Chamados[id].Mensagens.Count);
        Assert.Equal(2, f.Salvamentos);
        f.VinculoAtendente.Desativar();
        Assert.Throws<AcessoNegadoException>(() => f.Aplicacao.ExecutarAsync(comando, "responder").GetAwaiter().GetResult());
        Assert.Equal(2, f.Salvamentos);
    }

    public static void ColegaVisualizaMasNaoRespondePeloSolicitanteNemAtende()
    {
        var f = new Cenario();
        var id = f.Aplicacao.AbrirAsync(f.Abertura(), "abrir").GetAwaiter().GetResult().Id;
        var colega = new Usuario(f.Empresa.Id, "Colega");
        f.Usuarios.Add(colega);
        f.Vinculos.Add(new(colega, f.Origem.Id, PapelSetor.Solicitante));
        f.Autor = colega;
        var acesso = new AutorizacaoChamado(f.Empresa.Id, f.Dados(colega.Id, [f.Atendimento.FilaGeralId]));
        Assert.True(acesso.PodeVisualizar(f.Chamados[id], colega.Id));
        Assert.Throws<AcessoNegadoException>(() => f.Executar(new ResponderSolicitante(id, "Não sou o solicitante")));
        Assert.Throws<AcessoNegadoException>(() => f.Executar(new AceitarChamado(id)));
        Assert.Equal(1, f.Salvamentos);
    }

    public static void GestorNaoRecebeAcessoRestritoAutomaticamente()
    {
        var f = new Cenario();
        f.Atendimento.FilaGeral.AutorizarAcessoRestrito(f.Atendente.Id);
        var pedido = f.Abertura() with { Visibilidade = Visibilidade.AcessoRestrito };
        var id = f.Aplicacao.AbrirAsync(pedido, "abrir").GetAwaiter().GetResult().Id;
        var gestor = new Usuario(f.Empresa.Id, "Gestor sem acesso restrito");
        f.Usuarios.Add(gestor);
        f.Vinculos.Add(new(gestor, f.Atendimento.Id, PapelSetor.Gestor));
        var acesso = new AutorizacaoChamado(f.Empresa.Id, f.Dados(gestor.Id, [f.Atendimento.FilaGeralId]));
        Assert.False(acesso.PodeVisualizar(f.Chamados[id], gestor.Id));
        acesso.ExigirGestaoSetor(gestor.Id, f.Atendimento.Id);
        f.Autor = gestor;
        Assert.Throws<AcessoNegadoException>(() => f.Executar(new ResponderEquipe(id, "Restrito")));
    }

    public static void ConfiguracaoExigeGestorEUsaDominioParaHierarquiaESla()
    {
        var f = new Cenario();
        Assert.Throws<AcessoNegadoException>(() => f.Configuracao.ExecutarAsync(new CriarFila(f.Atendimento.Id, "Nova"), "fila").GetAwaiter().GetResult());
        f.Autor = f.Atendente;
        f.VinculoAtendente.ConcederPapel(PapelSetor.Gestor);
        var criada = f.Configuracao.ExecutarAsync(new CriarCategoria(f.Atendimento.Id, "Subcategoria", f.Categoria.Id), "categoria").GetAwaiter().GetResult();
        Assert.Equal(f.Categoria.Id, f.Atendimento.Categorias.Single(c => c.Id == criada.RegistroId).CategoriaPaiId);
        f.Configuracao.ExecutarAsync(new DefinirMetaSla(f.Atendimento.Id, Prioridade.Media, TipoSla.Resolucao, 16), "sla").GetAwaiter().GetResult();
        Assert.Equal(16m, f.Atendimento.Sla.ObterMeta(Prioridade.Media, TipoSla.Resolucao).Prazo.Valor);
        var outraEmpresa = new Empresa("Externa", new("UTC"));
        Assert.Throws<AcessoNegadoException>(() => f.Configuracao.ExecutarAsync(new CriarFila(outraEmpresa.CriarSetor("TI").Id, "Nova"), "externa").GetAwaiter().GetResult());
        Assert.Equal(2, f.Salvamentos);
    }

    public static void OrigemNaoPodeSerForjadaNemChaveReutilizadaComOutroComando()
    {
        var f = new Cenario();
        Assert.Throws<AcessoNegadoException>(() => f.Aplicacao.AbrirAsync(f.Abertura() with { SetorOrigemId = f.Atendimento.Id }, "origem").GetAwaiter().GetResult());
        var id = f.Aplicacao.AbrirAsync(f.Abertura(), "abrir").GetAwaiter().GetResult().Id;
        f.Executar(new ResponderSolicitante(id, "Primeira"), "mesma");
        Assert.Throws<RegraNegocioException>(() => f.Executar(new ResponderSolicitante(id, "Outra"), "mesma"));
        Assert.Equal(1, f.Chamados[id].Mensagens.Count);
    }

    private sealed class Cenario : IEmpresaAtual, IUsuarioAtual, IChamadoRepository, IConfiguracaoRepository, IAcessoRepository, IExecutorComandoIdempotente, IUnidadeTrabalho, IAutorizacaoEmpresa
    {
        public Empresa Empresa { get; } = new("Empresa", new("UTC"));
        public Guid EmpresaId => Empresa.Id;
        public Guid UsuarioId => Autor.Id;
        public Setor Origem { get; }
        public Setor Atendimento { get; }
        public Categoria Categoria { get; }
        public Usuario Solicitante { get; }
        public Usuario Atendente { get; }
        public Usuario Autor { get; set; }
        public VinculoSetor VinculoAtendente { get; }
        public List<Usuario> Usuarios { get; } = [];
        public List<Setor> Setores { get; } = [];
        public bool Administrador { get; set; }
        public List<VinculoSetor> Vinculos { get; } = [];
        public Dictionary<Guid, Chamado> Chamados { get; } = [];
        public int Salvamentos { get; private set; }
        private readonly Dictionary<string, (string Conteudo, string Resultado)> _resultados = [];
        private readonly Relogio _relogio = new();
        public Relogio Relogio => _relogio;
        public ChamadoAplicacao Aplicacao { get; }
        public ConfiguracaoSetorAplicacao Configuracao { get; }
        public ConfiguracaoEmpresaAplicacao ConfiguracaoEmpresa { get; }
        public Cenario()
        {
            foreach (var dia in Enum.GetValues<DayOfWeek>()) Empresa.Calendario.DefinirExpediente(dia, [new(new(8, 0), new(17, 0))]);
            Origem = Empresa.CriarSetor("Operações"); Atendimento = Empresa.CriarSetor("TI");
            Setores.AddRange([Origem, Atendimento]);
            foreach (var prioridade in Enum.GetValues<Prioridade>())
                foreach (var tipo in Enum.GetValues<TipoSla>()) Atendimento.Sla.DefinirMeta(prioridade, tipo, new(8));
            Categoria = Atendimento.CriarCategoria("Licenças");
            Solicitante = new(Empresa.Id, "Solicitante"); Atendente = new(Empresa.Id, "Atendente"); Autor = Solicitante;
            Usuarios.AddRange([Solicitante, Atendente]);
            Vinculos.Add(new(Solicitante, Origem.Id, PapelSetor.Solicitante));
            VinculoAtendente = new(Atendente, Atendimento.Id, PapelSetor.Atendente); Vinculos.Add(VinculoAtendente);
            Atendimento.FilaGeral.AdicionarMembro(VinculoAtendente);
            Aplicacao = new(this, this, this, this, this, this, this, _relogio);
            Configuracao = new(this, this, this, this, this, this, _relogio);
            ConfiguracaoEmpresa = new(this, this, this, this, this, this, this);
        }
        public AbrirChamado Abertura() => new(Origem.Id, Atendimento.Id, Categoria.Id, "Acesso", "Solicitação", Prioridade.Media, Visibilidade.CompartilhadoComSetor);
        public ResultadoChamado Executar(ComandoChamado comando, string? chave = null)
        {
            _relogio.Agora = _relogio.Agora.AddMinutes(1);
            return Aplicacao.ExecutarAsync(comando, chave ?? Guid.NewGuid().ToString()).GetAwaiter().GetResult();
        }
        public DadosAcesso Dados(Guid usuarioId, IReadOnlyCollection<Guid> filas) => new(Usuarios.Single(u => u.Id == usuarioId),
            Vinculos.Where(v => v.UsuarioId == usuarioId).ToList(), Setores.Select(s => new SetorAcesso(s.Id, s.Ativo)).ToList(),
            Setores.SelectMany(s => s.Filas).Where(f => filas.Contains(f.Id)).ToList());
        public Task<DadosAcesso?> ObterDadosAsync(Guid usuarioId, IReadOnlyCollection<Guid> filas, CancellationToken cancellationToken = default) => Task.FromResult<DadosAcesso?>(Dados(usuarioId, filas));
        public Task<ReferenciaAcessoChamado?> ObterReferenciaAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Chamados.TryGetValue(id, out var c) ? AutorizacaoChamado.Referencia(c) : null);
        public Task<Chamado?> ObterParaAtualizacaoAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(Chamados.GetValueOrDefault(id));
        public void Adicionar(Chamado c) => Chamados.Add(c.Id, c);
        public void Adicionar(Setor setor) => Setores.Add(setor);
        public void Adicionar(Usuario u) => Usuarios.Add(u);
        public void Adicionar(VinculoSetor vinculo) => Vinculos.Add(vinculo);
        public Task<Empresa?> ObterEmpresaAsync(CancellationToken cancellationToken = default) => Task.FromResult<Empresa?>(Empresa);
        public Task<Setor?> ObterSetorAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(Setores.SingleOrDefault(s => s.Id == id));
        public Task<Usuario?> ObterUsuarioAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(Usuarios.SingleOrDefault(u => u.Id == id));
        public Task<VinculoSetor?> ObterVinculoAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(Vinculos.SingleOrDefault(v => v.Id == id));
        public Task BloquearVinculosUsuarioAsync(Guid usuarioId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task BloquearSetorAsync(Guid setorId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task ExigirAdministracaoAsync(Guid empresaId, Guid usuarioId, CancellationToken cancellationToken = default)
        {
            if (!Administrador || empresaId != EmpresaId || usuarioId != UsuarioId) throw new AcessoNegadoException("Administração exigida.");
            return Task.CompletedTask;
        }
        public Task<int> SalvarAsync(CancellationToken cancellationToken = default) { Salvamentos++; return Task.FromResult(1); }
        public async Task<string> ExecutarAsync(string chave, string conteudoCanonico, Func<CancellationToken, Task<string>> comando, CancellationToken cancellationToken = default)
        {
            var identidade = UsuarioId + ":" + chave;
            if (_resultados.TryGetValue(identidade, out var anterior))
            {
                if (anterior.Conteudo != conteudoCanonico) throw new RegraNegocioException("Chave reutilizada com outros dados.");
                return anterior.Resultado;
            }
            var resultado = await comando(cancellationToken);
            _resultados.Add(identidade, (conteudoCanonico, resultado));
            return resultado;
        }
    }
    private sealed class Relogio : TimeProvider
    {
        public DateTimeOffset Agora { get; set; } = new(2026, 10, 7, 8, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Agora;
    }
}
