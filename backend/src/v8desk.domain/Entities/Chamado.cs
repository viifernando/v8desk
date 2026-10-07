using System.Globalization;

namespace v8desk.domain.Entities;

public sealed class Chamado
{
    private static readonly StatusChamado[] StatusAtivos =
    [
        StatusChamado.AguardandoTriagem,
        StatusChamado.Aceito,
        StatusChamado.EmAtendimento,
        StatusChamado.AguardandoInformacao
    ];

    private readonly List<EventoChamado> _eventos = new();
    private readonly List<Mensagem> _mensagens = new();
    private readonly List<CicloAtendimento> _ciclos = new();

    public Guid Id { get; }
    public Guid EmpresaId { get; }
    public Guid SolicitanteId { get; }
    public ReferenciaHistorica SetorOrigemNaAbertura { get; }
    public string Titulo { get; private set; }
    public string DescricaoOriginal { get; }
    public StatusChamado Status { get; private set; }
    public Prioridade Prioridade { get; private set; }
    public Visibilidade Visibilidade { get; private set; }
    public Guid CategoriaId { get; private set; }
    public ContextoAtendimento ContextoAtual { get; private set; }
    public Guid SetorAtualId => ContextoAtual.Setor.Id;
    public Guid FilaAtualId => ContextoAtual.Fila.Id;
    public Guid? ResponsavelId => ContextoAtual.Responsavel?.Id;
    public MetasSlaAplicadas MetasSla { get; private set; }
    public PoliticaCicloVida CicloVidaAplicado { get; private set; }
    public DateTimeOffset AbertoEm { get; }
    public DateTimeOffset? LimiteValidacao { get; private set; }
    public DateTimeOffset? LembreteValidacaoEm { get; private set; }
    public DateTimeOffset? LimiteReabertura { get; private set; }
    public long Versao { get; private set; }
    public IReadOnlyList<EventoChamado> Eventos => _eventos.AsReadOnly();
    public IReadOnlyList<Mensagem> Mensagens => _mensagens.AsReadOnly();
    public IReadOnlyList<CicloAtendimento> Ciclos => _ciclos.AsReadOnly();
    public CicloAtendimento CicloAtual => _ciclos[^1];

    private Chamado(Guid empresaId, Guid solicitanteId, ReferenciaHistorica setorOrigem,
        string titulo, string descricao, Prioridade prioridade, Visibilidade visibilidade,
        Guid categoriaId, ContextoAtendimento contextoAtual, MetasSlaAplicadas metasSla,
        PoliticaCicloVida cicloVida, DateTimeOffset abertoEm)
    {
        Id = Guid.CreateVersion7();
        EmpresaId = empresaId;
        SolicitanteId = solicitanteId;
        SetorOrigemNaAbertura = setorOrigem;
        Titulo = titulo;
        DescricaoOriginal = descricao;
        Status = StatusChamado.AguardandoTriagem;
        Prioridade = prioridade;
        Visibilidade = visibilidade;
        CategoriaId = categoriaId;
        ContextoAtual = contextoAtual;
        MetasSla = metasSla;
        CicloVidaAplicado = cicloVida;
        AbertoEm = abertoEm;
    }

    public static Chamado Abrir(DadosAbertura dados, ContextoOperacao contexto,
        ConfiguracaoAplicada configuracao)
    {
        ArgumentNullException.ThrowIfNull(dados);
        ArgumentNullException.ThrowIfNull(contexto);
        ArgumentNullException.ThrowIfNull(configuracao);
        ArgumentNullException.ThrowIfNull(dados.SetorOrigem);
        ArgumentNullException.ThrowIfNull(dados.Fila);

        var empresaId = Guarda.Identificador(dados.EmpresaId, "a empresa");
        var solicitanteId = Guarda.Identificador(dados.SolicitanteId, "o solicitante");
        var categoriaId = Guarda.Identificador(dados.CategoriaId, "a categoria");
        if (contexto.AutorId != solicitanteId)
            throw new AcessoNegadoException("Somente o próprio solicitante pode abrir o chamado.");

        var titulo = Guarda.Texto(dados.Titulo, "o título");
        var descricao = Guarda.Texto(dados.Descricao, "a descrição");
        var prioridade = Guarda.Definido(dados.Prioridade, "a prioridade");
        var visibilidade = Guarda.Definido(dados.Visibilidade, "a visibilidade");
        var metas = MetasSlaAplicadas.De(configuracao.PoliticaSla, configuracao.Setor.Id,
            prioridade, configuracao.Calendario.Versao);

        var chamado = new Chamado(empresaId, solicitanteId, dados.SetorOrigem, titulo, descricao,
            prioridade, visibilidade, categoriaId,
            new ContextoAtendimento(configuracao.Setor, dados.Fila, null),
            metas, configuracao.CicloVida, contexto.Agora);

        var ciclo = new CicloAtendimento(1, contexto.Agora);
        chamado._ciclos.Add(ciclo);
        ciclo.IniciarPeriodo(StatusChamado.AguardandoTriagem, chamado.ContextoAtual, contexto.Agora);
        ciclo.IniciarSla(TipoSla.PrimeiraResposta, metas, contexto.Agora);
        ciclo.IniciarSla(TipoSla.Resolucao, metas, contexto.Agora);

        chamado.RegistrarEvento(TipoEventoChamado.Aberto, contexto.AutorId, contexto.Agora, null,
            SemDados,
            Dados(("Status", chamado.Status), ("Setor", chamado.SetorAtualId),
                ("Fila", chamado.FilaAtualId), ("Categoria", categoriaId),
                ("Prioridade", prioridade), ("Visibilidade", visibilidade)));

        return chamado;
    }

    public void Aceitar(ContextoOperacao contexto, IAutorizacaoChamado acesso)
    {
        ExigirAtendimento(contexto, acesso);
        ExigirStatus(StatusChamado.AguardandoTriagem);

        var anterior = Status;
        MudarEtapa(StatusChamado.Aceito, ContextoAtual, contexto.Agora);
        RegistrarEvento(TipoEventoChamado.Aceito, contexto.AutorId, contexto.Agora, null,
            Dados(("Status", anterior)), Dados(("Status", Status)));
    }

    public void Assumir(Usuario atendente, Fila fila, ContextoOperacao contexto)
    {
        ArgumentNullException.ThrowIfNull(atendente);
        ArgumentNullException.ThrowIfNull(fila);
        ArgumentNullException.ThrowIfNull(contexto);
        if (atendente.Id != contexto.AutorId)
            throw new AcessoNegadoException("O chamado só pode ser assumido pelo próprio atendente.");

        ExigirStatus(StatusChamado.AguardandoTriagem, StatusChamado.Aceito);
        if (ResponsavelId is not null)
            throw new RegraNegocioException("O chamado já possui responsável.");
        ExigirAtendenteHabilitado(atendente, fila);

        var anterior = Status;
        MudarEtapa(StatusChamado.Aceito,
            ContextoAtual with { Responsavel = new ReferenciaHistorica(atendente.Id, atendente.Nome) },
            contexto.Agora);
        RegistrarEvento(TipoEventoChamado.Assumido, contexto.AutorId, contexto.Agora, null,
            Dados(("Status", anterior), ("Responsavel", null)),
            Dados(("Status", Status), ("Responsavel", ResponsavelId)));
    }

    public void IniciarAtendimento(ContextoOperacao contexto, IAutorizacaoChamado acesso)
    {
        ExigirAtendimento(contexto, acesso);
        ExigirStatus(StatusChamado.Aceito);
        if (ResponsavelId != contexto.AutorId)
            throw new AcessoNegadoException("Somente o responsável pode iniciar o atendimento.");

        var anterior = Status;
        MudarEtapa(StatusChamado.EmAtendimento, ContextoAtual, contexto.Agora);
        RegistrarEvento(TipoEventoChamado.AtendimentoIniciado, contexto.AutorId, contexto.Agora, null,
            Dados(("Status", anterior)), Dados(("Status", Status)));
    }

    public void TrocarResponsavel(Usuario? destino, Fila filaAtual, string motivo,
        ContextoOperacao contexto, IAutorizacaoChamado acesso)
    {
        ExigirAtendimento(contexto, acesso);
        ArgumentNullException.ThrowIfNull(filaAtual);
        ExigirChamadoAtivo();
        var motivoValido = Guarda.Texto(motivo, "o motivo");
        ExigirFilaAtual(filaAtual);
        if (destino?.Id == ResponsavelId)
            throw new RegraNegocioException("O responsável informado já é o atual.");
        if (destino is not null)
            ExigirAtendenteHabilitado(destino, filaAtual);

        var novoStatus = Status switch
        {
            StatusChamado.EmAtendimento => StatusChamado.Aceito,
            StatusChamado.AguardandoTriagem when destino is not null => StatusChamado.Aceito,
            _ => Status
        };
        var antes = Dados(("Status", Status), ("Responsavel", ResponsavelId));

        MudarEtapa(novoStatus,
            ContextoAtual with
            {
                Responsavel = destino is null ? null : new ReferenciaHistorica(destino.Id, destino.Nome)
            },
            contexto.Agora);
        RegistrarEvento(TipoEventoChamado.ResponsavelAlterado, contexto.AutorId, contexto.Agora,
            motivoValido, antes, Dados(("Status", Status), ("Responsavel", ResponsavelId)));
    }

    public void SolicitarInformacao(string pergunta, ContextoOperacao contexto,
        IAutorizacaoChamado acesso)
    {
        ExigirAtendimento(contexto, acesso);
        ExigirStatus(StatusChamado.AguardandoTriagem, StatusChamado.Aceito,
            StatusChamado.EmAtendimento);
        var texto = Guarda.Texto(pergunta, "a pergunta");

        var anterior = Status;
        var mensagem = RegistrarMensagem(contexto.AutorId, TipoMensagem.Publica, texto, contexto.Agora);
        CicloAtual.FinalizarEsperasDeResposta(contexto.Agora, MotivoFinalizacaoSla.Respondido);
        CicloAtual.PausarResolucao(contexto.Agora, "Aguardando solicitante");
        MudarEtapa(StatusChamado.AguardandoInformacao, ContextoAtual, contexto.Agora);
        RegistrarEvento(TipoEventoChamado.InformacaoSolicitada, contexto.AutorId, contexto.Agora, null,
            Dados(("Status", anterior)), Dados(("Status", Status), ("Mensagem", mensagem.Id)));
    }

    public void ResponderSolicitante(string texto, ContextoOperacao contexto)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        ExigirSolicitante(contexto.AutorId);
        ExigirStatus(StatusChamado.AguardandoTriagem, StatusChamado.Aceito,
            StatusChamado.EmAtendimento, StatusChamado.AguardandoInformacao, StatusChamado.Resolvido);
        var conteudo = Guarda.Texto(texto, "o texto");

        var anterior = Status;
        var mensagem = RegistrarMensagem(contexto.AutorId, TipoMensagem.Publica, conteudo, contexto.Agora);

        if (Status == StatusChamado.AguardandoInformacao)
        {
            CicloAtual.RetomarResolucao(contexto.Agora);
            MudarEtapa(StatusChamado.Aceito, ContextoAtual, contexto.Agora);
        }

        if (Status != StatusChamado.Resolvido)
            CicloAtual.IniciarProximaRespostaSeNecessario(MetasSla, contexto.Agora);

        RegistrarEvento(TipoEventoChamado.SolicitanteRespondeu, contexto.AutorId, contexto.Agora, null,
            Dados(("Status", anterior)), Dados(("Status", Status), ("Mensagem", mensagem.Id)));
    }

    public void ResponderEquipe(string texto, ContextoOperacao contexto, IAutorizacaoChamado acesso)
    {
        ExigirAtendimento(contexto, acesso);
        ExigirStatus(StatusChamado.AguardandoTriagem, StatusChamado.Aceito,
            StatusChamado.EmAtendimento, StatusChamado.AguardandoInformacao, StatusChamado.Resolvido);
        var conteudo = Guarda.Texto(texto, "o texto");

        var mensagem = RegistrarMensagem(contexto.AutorId, TipoMensagem.Publica, conteudo, contexto.Agora);
        CicloAtual.FinalizarEsperasDeResposta(contexto.Agora, MotivoFinalizacaoSla.Respondido);
        RegistrarEvento(TipoEventoChamado.EquipeRespondeu, contexto.AutorId, contexto.Agora, null,
            SemDados, Dados(("Mensagem", mensagem.Id)));
    }

    public void AdicionarNotaInterna(string texto, ContextoOperacao contexto,
        IAutorizacaoChamado acesso)
    {
        ExigirAtendimento(contexto, acesso);
        var conteudo = Guarda.Texto(texto, "o texto");

        var mensagem = RegistrarMensagem(contexto.AutorId, TipoMensagem.NotaInterna, conteudo, contexto.Agora);
        RegistrarEvento(TipoEventoChamado.NotaInternaAdicionada, contexto.AutorId, contexto.Agora, null,
            SemDados, Dados(("Mensagem", mensagem.Id)));
    }

    public void AlterarPrioridade(Prioridade prioridade, string motivo, PoliticaSla politica,
        ContextoOperacao contexto, IAutorizacaoChamado acesso)
    {
        ExigirAtendimento(contexto, acesso);
        ArgumentNullException.ThrowIfNull(politica);
        ExigirChamadoAtivo();
        Guarda.Definido(prioridade, "a prioridade");
        var motivoValido = Guarda.Texto(motivo, "o motivo");
        if (prioridade == Prioridade)
            throw new RegraNegocioException("A prioridade informada já é a atual.");

        var metas = MetasSlaAplicadas.De(politica, SetorAtualId, prioridade, MetasSla.VersaoCalendario);
        var anterior = Prioridade;

        Prioridade = prioridade;
        MetasSla = metas;
        CicloAtual.AplicarMetas(metas, contexto.Agora);
        RegistrarEvento(TipoEventoChamado.PrioridadeAlterada, contexto.AutorId, contexto.Agora,
            motivoValido, Dados(("Prioridade", anterior)), Dados(("Prioridade", Prioridade)));
    }

    public void AlterarCategoria(Categoria categoria, string motivo, ContextoOperacao contexto,
        IAutorizacaoChamado acesso)
    {
        ExigirAtendimento(contexto, acesso);
        ArgumentNullException.ThrowIfNull(categoria);
        ExigirChamadoAtivo();
        var motivoValido = Guarda.Texto(motivo, "o motivo");
        if (categoria.SetorId != SetorAtualId)
            throw new RegraNegocioException("A categoria deve pertencer ao setor atual do chamado.");
        if (!categoria.Ativa)
            throw new RegraNegocioException("A categoria informada está inativa.");
        if (categoria.Id == CategoriaId)
            throw new RegraNegocioException("A categoria informada já é a atual.");

        var anterior = CategoriaId;
        CategoriaId = categoria.Id;
        RegistrarEvento(TipoEventoChamado.CategoriaAlterada, contexto.AutorId, contexto.Agora,
            motivoValido, Dados(("Categoria", anterior)), Dados(("Categoria", CategoriaId)));
    }

    public void Transferir(Fila destino, Categoria categoriaDestino, string motivo,
        ConfiguracaoAplicada configuracao, ContextoOperacao contexto, IAutorizacaoChamado acesso)
    {
        ArgumentNullException.ThrowIfNull(destino);
        ArgumentNullException.ThrowIfNull(categoriaDestino);
        ArgumentNullException.ThrowIfNull(configuracao);
        ArgumentNullException.ThrowIfNull(contexto);
        ArgumentNullException.ThrowIfNull(acesso);
        acesso.ExigirTransferencia(this, contexto.AutorId, destino);
        ExigirChamadoAtivo();
        var motivoValido = Guarda.Texto(motivo, "o motivo da transferência");

        if (!destino.Ativa)
            throw new RegraNegocioException("A fila de destino está inativa.");
        if (destino.Id == FilaAtualId)
            throw new RegraNegocioException("O chamado já está na fila de destino.");
        if (configuracao.Setor.Id != destino.SetorId)
            throw new RegraNegocioException("A configuração aplicada não corresponde ao setor de destino.");
        if (categoriaDestino.SetorId != destino.SetorId || !categoriaDestino.Ativa)
            throw new RegraNegocioException("A categoria de destino deve estar ativa e pertencer ao setor de destino.");
        if (Visibilidade == Visibilidade.AcessoRestrito && !destino.TemAtendenteAutorizadoParaRestritos())
            throw new RegraNegocioException("A fila de destino não possui atendente autorizado para chamados restritos.");

        var mesmoSetor = destino.SetorId == SetorAtualId;
        var novasMetas = mesmoSetor
            ? null
            : MetasSlaAplicadas.De(configuracao.PoliticaSla, destino.SetorId, Prioridade,
                configuracao.Calendario.Versao);
        var filaDestino = new ReferenciaHistorica(destino.Id, destino.Nome);
        var antes = Dados(("Status", Status), ("Setor", SetorAtualId), ("Fila", FilaAtualId),
            ("Categoria", CategoriaId), ("Responsavel", ResponsavelId));

        if (novasMetas is null)
        {
            var manterResponsavel = ResponsavelId is { } responsavelId && PodeAtenderNaFila(responsavelId, destino);
            var removerResponsavel = ResponsavelId is not null && !manterResponsavel;
            var novoStatus = removerResponsavel && Status is StatusChamado.Aceito or StatusChamado.EmAtendimento
                ? StatusChamado.AguardandoTriagem
                : Status;

            CategoriaId = categoriaDestino.Id;
            MudarEtapa(novoStatus,
                ContextoAtual with
                {
                    Fila = filaDestino,
                    Responsavel = manterResponsavel ? ContextoAtual.Responsavel : null
                },
                contexto.Agora);
        }
        else
        {
            var aguardavaResposta = CicloAtual.AguardandoRespostaEquipe;

            CicloAtual.InterromperSlas(contexto.Agora, MotivoFinalizacaoSla.Transferencia);
            CategoriaId = categoriaDestino.Id;
            MetasSla = novasMetas;
            MudarEtapa(StatusChamado.AguardandoTriagem,
                new ContextoAtendimento(configuracao.Setor, filaDestino, null), contexto.Agora);
            CicloAtual.IniciarSla(TipoSla.Resolucao, novasMetas, contexto.Agora);
            if (aguardavaResposta)
                CicloAtual.IniciarSla(TipoSla.ProximaResposta, novasMetas, contexto.Agora);
        }

        RegistrarEvento(TipoEventoChamado.Transferido, contexto.AutorId, contexto.Agora, motivoValido,
            antes,
            Dados(("Status", Status), ("Setor", SetorAtualId), ("Fila", FilaAtualId),
                ("Categoria", CategoriaId), ("Responsavel", ResponsavelId)));
    }

    public void AlterarVisibilidade(Visibilidade visibilidade, string motivo, Fila filaAtual,
        ContextoOperacao contexto, IAutorizacaoChamado acesso)
    {
        ExigirAtendimento(contexto, acesso);
        ArgumentNullException.ThrowIfNull(filaAtual);
        ExigirChamadoAtivo();
        Guarda.Definido(visibilidade, "a visibilidade");
        var motivoValido = Guarda.Texto(motivo, "o motivo");
        ExigirFilaAtual(filaAtual);
        if (visibilidade == Visibilidade)
            throw new RegraNegocioException("A visibilidade informada já é a atual.");

        if (visibilidade == Visibilidade.AcessoRestrito)
        {
            if (!filaAtual.TemAtendenteAutorizadoParaRestritos())
                throw new RegraNegocioException("A fila atual não possui atendente autorizado para chamados restritos.");
            if (ResponsavelId is { } responsavelId && !filaAtual.PodeAcessarRestrito(responsavelId))
                throw new RegraNegocioException("O responsável atual não está autorizado a chamados restritos.");
        }

        var anterior = Visibilidade;
        Visibilidade = visibilidade;
        RegistrarEvento(TipoEventoChamado.VisibilidadeAlterada, contexto.AutorId, contexto.Agora,
            motivoValido, Dados(("Visibilidade", anterior)), Dados(("Visibilidade", Visibilidade)));
    }

    public void Resolver(string descricaoSolucao, ConfiguracaoAplicada configuracao,
        ContextoOperacao contexto, IAutorizacaoChamado acesso)
    {
        ExigirAtendimento(contexto, acesso);
        ArgumentNullException.ThrowIfNull(configuracao);
        ExigirChamadoAtivo();
        var descricao = Guarda.Texto(descricaoSolucao, "a descrição da solução");
        ExigirConfiguracaoDoSetorAtual(configuracao);

        var cicloVida = configuracao.CicloVida;
        var limite = configuracao.Calendario.SomarHorasUteis(contexto.Agora, cicloVida.PrazoValidacao);
        DateTimeOffset? lembrete = cicloVida.DiasUteisAntecedenciaLembrete == 0
            ? null
            : configuracao.Calendario.SubtrairDiasUteis(limite, cicloVida.DiasUteisAntecedenciaLembrete);
        if (lembrete <= contexto.Agora)
            lembrete = null;

        var anterior = Status;
        var mensagem = RegistrarMensagem(contexto.AutorId, TipoMensagem.Publica, descricao, contexto.Agora);
        CicloAtual.FinalizarEsperasDeResposta(contexto.Agora, MotivoFinalizacaoSla.Resolvido);
        CicloAtual.FinalizarResolucao(contexto.Agora, MotivoFinalizacaoSla.Resolvido);
        CicloAtual.RegistrarSolucao(new Solucao(contexto.AutorId, descricao, contexto.Agora));
        CicloVidaAplicado = cicloVida;
        LimiteValidacao = limite;
        LembreteValidacaoEm = lembrete;
        MudarEtapa(StatusChamado.Resolvido, ContextoAtual, contexto.Agora);
        RegistrarEvento(TipoEventoChamado.Resolvido, contexto.AutorId, contexto.Agora, null,
            Dados(("Status", anterior)),
            Dados(("Status", Status), ("Mensagem", mensagem.Id), ("LimiteValidacao", LimiteValidacao)));
    }

    public void ConfirmarSolucao(ContextoOperacao contexto)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        ExigirSolicitante(contexto.AutorId);
        ExigirStatus(StatusChamado.Resolvido);

        Encerrar(MotivoEncerramento.ConfirmacaoSolicitante, contexto.AutorId, contexto.Agora,
            TipoEventoChamado.SolucaoConfirmada);
    }

    public void InformarSolucaoInsuficiente(string motivo, ContextoOperacao contexto,
        ConfiguracaoAplicada configuracao)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        ArgumentNullException.ThrowIfNull(configuracao);
        ExigirSolicitante(contexto.AutorId);
        ExigirStatus(StatusChamado.Resolvido);
        var motivoValido = Guarda.Texto(motivo, "o motivo");
        ExigirConfiguracaoDoSetorAtual(configuracao);

        var metas = MetasSlaAplicadas.De(configuracao.PoliticaSla, SetorAtualId, Prioridade,
            configuracao.Calendario.Versao);
        var anterior = Status;

        var mensagem = RegistrarMensagem(contexto.AutorId, TipoMensagem.Publica, motivoValido, contexto.Agora);
        CicloAtual.RejeitarSolucao(motivoValido, contexto.Agora);
        MetasSla = metas;
        CicloAtual.IniciarSla(TipoSla.Resolucao, metas, contexto.Agora);
        CicloAtual.IniciarSla(TipoSla.ProximaResposta, metas, contexto.Agora);
        LimiteValidacao = null;
        LembreteValidacaoEm = null;
        MudarEtapa(StatusChamado.Aceito, ContextoAtual, contexto.Agora);
        RegistrarEvento(TipoEventoChamado.SolucaoInsuficiente, contexto.AutorId, contexto.Agora,
            motivoValido, Dados(("Status", anterior)),
            Dados(("Status", Status), ("Mensagem", mensagem.Id)));
    }

    public void EncerrarPorPrazo(DateTimeOffset agora)
    {
        if (Status != StatusChamado.Resolvido || LimiteValidacao is not { } limite || agora < limite)
            return;

        Encerrar(MotivoEncerramento.PrazoExpirado, null, agora, TipoEventoChamado.EncerradoPorPrazo);
    }

    public void Reabrir(string motivo, Fila filaAtual, Usuario? responsavelAtual,
        ConfiguracaoAplicada configuracao, ContextoOperacao contexto)
    {
        ArgumentNullException.ThrowIfNull(filaAtual);
        ArgumentNullException.ThrowIfNull(configuracao);
        ArgumentNullException.ThrowIfNull(contexto);
        ExigirSolicitante(contexto.AutorId);
        ExigirStatus(StatusChamado.Encerrado);
        if (LimiteReabertura is not { } limite || contexto.Agora > limite)
            throw new RegraNegocioException("O prazo para reabertura expirou.");

        var motivoValido = Guarda.Texto(motivo, "o motivo da reabertura");
        ExigirFilaAtual(filaAtual);
        if (!filaAtual.Ativa)
            throw new RegraNegocioException("A fila do chamado está inativa.");
        ExigirConfiguracaoDoSetorAtual(configuracao);
        if (responsavelAtual is not null && responsavelAtual.Id != ResponsavelId)
            throw new RegraNegocioException("O usuário informado não é o responsável do chamado.");

        var manterResponsavel = responsavelAtual is { Ativo: true, DisponivelParaAtendimento: true }
            && PodeAtenderNaFila(responsavelAtual.Id, filaAtual);
        var metas = MetasSlaAplicadas.De(configuracao.PoliticaSla, SetorAtualId, Prioridade,
            configuracao.Calendario.Versao);
        var antes = Dados(("Status", Status), ("Responsavel", ResponsavelId));

        var ciclo = new CicloAtendimento(CicloAtual.Numero + 1, contexto.Agora);
        _ciclos.Add(ciclo);
        var mensagem = RegistrarMensagem(contexto.AutorId, TipoMensagem.Publica, motivoValido, contexto.Agora);
        MetasSla = metas;
        MudarEtapa(StatusChamado.Aceito,
            ContextoAtual with { Responsavel = manterResponsavel ? ContextoAtual.Responsavel : null },
            contexto.Agora);
        ciclo.IniciarSla(TipoSla.Resolucao, metas, contexto.Agora);
        ciclo.IniciarSla(TipoSla.ProximaResposta, metas, contexto.Agora);
        LimiteReabertura = null;
        LimiteValidacao = null;
        RegistrarEvento(TipoEventoChamado.Reaberto, contexto.AutorId, contexto.Agora, motivoValido,
            antes,
            Dados(("Status", Status), ("Responsavel", ResponsavelId), ("Ciclo", ciclo.Numero),
                ("Mensagem", mensagem.Id)));
    }

    public void Cancelar(string motivo, ContextoOperacao contexto, IAutorizacaoChamado acesso)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        ArgumentNullException.ThrowIfNull(acesso);
        if (contexto.AutorId != SolicitanteId)
            acesso.ExigirCancelamento(this, contexto.AutorId);
        ExigirChamadoAtivo();
        var motivoValido = Guarda.Texto(motivo, "o motivo do cancelamento");

        var anterior = Status;
        var mensagem = RegistrarMensagem(contexto.AutorId, TipoMensagem.Publica, motivoValido, contexto.Agora);
        CicloAtual.InterromperSlas(contexto.Agora, MotivoFinalizacaoSla.Cancelado);
        MudarEtapa(StatusChamado.Cancelado, ContextoAtual, contexto.Agora);
        CicloAtual.Encerrar(contexto.Agora, MotivoEncerramento.Cancelamento);
        LembreteValidacaoEm = null;
        RegistrarEvento(TipoEventoChamado.Cancelado, contexto.AutorId, contexto.Agora, motivoValido,
            Dados(("Status", anterior)), Dados(("Status", Status), ("Mensagem", mensagem.Id)));
    }

    public void Avaliar(int nota, string? comentario, ContextoOperacao contexto,
        DateTimeOffset limiteAvaliacao)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        ExigirSolicitante(contexto.AutorId);
        ExigirStatus(StatusChamado.Encerrado);
        if (contexto.Agora > limiteAvaliacao)
            throw new RegraNegocioException("O prazo para avaliação expirou.");

        var avaliacao = new Avaliacao(nota, comentario, SolicitanteId, contexto.Agora);
        CicloAtual.RegistrarAvaliacao(avaliacao);
        RegistrarEvento(TipoEventoChamado.Avaliado, contexto.AutorId, contexto.Agora, null,
            SemDados, Dados(("Ciclo", CicloAtual.Numero), ("Nota", avaliacao.Nota)));
    }

    public void Anexar(Guid mensagemId, Anexo anexo, ContextoOperacao contexto)
    {
        ArgumentNullException.ThrowIfNull(anexo);
        ArgumentNullException.ThrowIfNull(contexto);
        if (Status is StatusChamado.Encerrado or StatusChamado.Cancelado)
            throw new RegraNegocioException("Chamado finalizado não aceita novos anexos.");

        var mensagem = ObterMensagem(mensagemId);
        if (anexo.AutorId != contexto.AutorId || mensagem.AutorId != contexto.AutorId)
            throw new AcessoNegadoException("Somente o autor da mensagem pode anexar arquivos.");

        mensagem.Anexar(anexo);
        RegistrarEvento(TipoEventoChamado.AnexoAdicionado, contexto.AutorId, contexto.Agora, null,
            SemDados, Dados(("Mensagem", mensagem.Id), ("Anexo", anexo.Id)));
    }

    public void CorrigirMensagem(Guid mensagemId, string novoTexto, string motivo,
        ContextoOperacao contexto)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        var mensagem = ObterMensagem(mensagemId);
        var textoAnterior = mensagem.TextoAtual;

        mensagem.RegistrarCorrecao(novoTexto, motivo, contexto);
        RegistrarEvento(TipoEventoChamado.MensagemCorrigida, contexto.AutorId, contexto.Agora,
            mensagem.Correcoes[^1].Motivo,
            Dados(("Mensagem", mensagem.Id), ("Texto", textoAnterior)),
            Dados(("Mensagem", mensagem.Id), ("Texto", mensagem.TextoAtual)));
    }

    private void Encerrar(MotivoEncerramento motivo, Guid? autorId, DateTimeOffset agora,
        TipoEventoChamado tipo)
    {
        var anterior = Status;
        MudarEtapa(StatusChamado.Encerrado, ContextoAtual, agora);
        CicloAtual.Encerrar(agora, motivo);
        LembreteValidacaoEm = null;
        LimiteReabertura = agora + CicloVidaAplicado.PrazoReabertura;
        RegistrarEvento(tipo, autorId, agora, null,
            Dados(("Status", anterior)),
            Dados(("Status", Status), ("LimiteReabertura", LimiteReabertura)));
    }

    private void MudarEtapa(StatusChamado status, ContextoAtendimento contextoAtendimento,
        DateTimeOffset agora)
    {
        if (status == Status && contextoAtendimento == ContextoAtual && CicloAtual.PeriodoAtual is not null)
            return;

        Status = status;
        ContextoAtual = contextoAtendimento;

        if (status is StatusChamado.Encerrado or StatusChamado.Cancelado)
            CicloAtual.FinalizarPeriodo(agora);
        else
            CicloAtual.IniciarPeriodo(status, contextoAtendimento, agora);
    }

    private Mensagem RegistrarMensagem(Guid autorId, TipoMensagem tipo, string texto,
        DateTimeOffset agora)
    {
        var mensagem = new Mensagem(autorId, tipo, texto, agora);
        _mensagens.Add(mensagem);
        return mensagem;
    }

    private Mensagem ObterMensagem(Guid mensagemId) =>
        _mensagens.FirstOrDefault(m => m.Id == mensagemId)
        ?? throw new RegraNegocioException("Mensagem não encontrada no chamado.");

    private void RegistrarEvento(TipoEventoChamado tipo, Guid? autorId, DateTimeOffset agora,
        string? motivo, IReadOnlyDictionary<string, string?> antes,
        IReadOnlyDictionary<string, string?> depois)
    {
        _eventos.Add(new EventoChamado(Guid.CreateVersion7(), Id, agora, autorId, tipo, motivo,
            antes, depois));
        Versao++;
    }

    private bool PodeAtenderNaFila(Guid usuarioId, Fila fila) =>
        Visibilidade == Visibilidade.AcessoRestrito
            ? fila.PodeAcessarRestrito(usuarioId)
            : fila.PodeAtender(usuarioId);

    private void ExigirAtendenteHabilitado(Usuario usuario, Fila fila)
    {
        ExigirFilaAtual(fila);
        if (!usuario.Ativo)
            throw new RegraNegocioException("Usuário inativo não pode atender chamados.");
        if (!PodeAtenderNaFila(usuario.Id, fila))
            throw new AcessoNegadoException("O usuário não está autorizado a atender este chamado na fila.");
    }

    private void ExigirAtendimento(ContextoOperacao contexto, IAutorizacaoChamado acesso)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        ArgumentNullException.ThrowIfNull(acesso);
        acesso.ExigirAtendimento(this, contexto.AutorId);
    }

    private void ExigirFilaAtual(Fila fila)
    {
        if (fila.Id != FilaAtualId)
            throw new RegraNegocioException("A fila informada não é a fila atual do chamado.");
    }

    private void ExigirConfiguracaoDoSetorAtual(ConfiguracaoAplicada configuracao)
    {
        if (configuracao.Setor.Id != SetorAtualId)
            throw new RegraNegocioException("A configuração aplicada não pertence ao setor atual do chamado.");
    }

    private void ExigirSolicitante(Guid autorId)
    {
        if (autorId != SolicitanteId)
            throw new AcessoNegadoException("Somente o solicitante pode realizar esta operação.");
    }

    private void ExigirChamadoAtivo() => ExigirStatus(StatusAtivos);

    private void ExigirStatus(params StatusChamado[] permitidos)
    {
        if (!permitidos.Contains(Status))
            throw new RegraNegocioException($"Operação não permitida com o chamado em {Status}.");
    }

    private static readonly IReadOnlyDictionary<string, string?> SemDados =
        new Dictionary<string, string?>();

    private static IReadOnlyDictionary<string, string?> Dados(params (string Chave, object? Valor)[] itens) =>
        itens.ToDictionary(i => i.Chave, i => Formatar(i.Valor));

    private static string? Formatar(object? valor) => valor switch
    {
        null => null,
        DateTimeOffset instante => instante.ToString("O", CultureInfo.InvariantCulture),
        IFormattable formatavel => formatavel.ToString(null, CultureInfo.InvariantCulture),
        _ => valor.ToString()
    };
}
