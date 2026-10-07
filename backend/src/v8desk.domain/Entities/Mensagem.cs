namespace v8desk.domain.Entities;

public sealed class Mensagem
{
#pragma warning disable CS8618 // Materialização: valores preenchidos pelo EF.
    private Mensagem() { }
#pragma warning restore CS8618

    private readonly List<Anexo> _anexos = new();
    private readonly List<CorrecaoMensagem> _correcoes = new();

    public Guid Id { get; private set; }
    public Guid AutorId { get; private set; }
    public TipoMensagem Tipo { get; private set; }
    public string TextoOriginal { get; private set; }
    public DateTimeOffset CriadaEm { get; private set; }
    public string TextoAtual => _correcoes.Count == 0 ? TextoOriginal : _correcoes[^1].Texto;
    public IReadOnlyList<Anexo> Anexos => _anexos.AsReadOnly();
    public IReadOnlyList<CorrecaoMensagem> Correcoes => _correcoes.AsReadOnly();

    internal Mensagem(Guid autorId, TipoMensagem tipo, string texto, DateTimeOffset criadaEm)
    {
        Id = Guid.CreateVersion7();
        AutorId = Guarda.Identificador(autorId, "o autor da mensagem");
        Tipo = Guarda.Definido(tipo, "o tipo da mensagem");
        TextoOriginal = Guarda.Texto(texto, "o texto da mensagem", 20000);
        CriadaEm = criadaEm;
    }

    internal void Anexar(Anexo anexo)
    {
        ArgumentNullException.ThrowIfNull(anexo);
        if (anexo.AutorId != AutorId)
            throw new AcessoNegadoException("Somente o autor da mensagem pode anexar arquivos.");
        if (_anexos.Any(a => a.Id == anexo.Id))
            throw new RegraNegocioException("O anexo já foi adicionado à mensagem.");

        _anexos.Add(anexo);
    }

    internal void RegistrarCorrecao(string novoTexto, string motivo, ContextoOperacao contexto)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        if (contexto.AutorId != AutorId)
            throw new AcessoNegadoException("Somente o autor pode corrigir a mensagem.");

        var texto = Guarda.Texto(novoTexto, "o novo texto", 20000);
        var motivoValido = Guarda.Texto(motivo, "o motivo da correção");
        if (texto == TextoAtual)
            throw new RegraNegocioException("O novo texto é igual ao atual.");

        _correcoes.Add(new CorrecaoMensagem(texto, motivoValido, contexto.AutorId, contexto.Agora));
    }
}
