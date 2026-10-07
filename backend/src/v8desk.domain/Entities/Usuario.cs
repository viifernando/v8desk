namespace v8desk.domain.Entities;

public sealed class Usuario
{
#pragma warning disable CS8618 // Materialização: valores preenchidos pelo EF.
    private Usuario() { }
#pragma warning restore CS8618

    public Guid Id { get; private set; }
    public Guid EmpresaId { get; private set; }
    public string Nome { get; private set; }
    public bool Ativo { get; private set; }
    public bool DisponivelParaAtendimento { get; private set; }

    public Usuario(Guid empresaId, string nome)
    {
        Id = Guid.CreateVersion7();
        EmpresaId = Guarda.Identificador(empresaId, "a empresa do usuário");
        Nome = Guarda.Texto(nome, "o nome do usuário", 200);
        Ativo = true;
        DisponivelParaAtendimento = true;
    }

    public void AtualizarIdentificacao(string nome)
    {
        Nome = Guarda.Texto(nome, "o nome do usuário", 200);
    }

    public void DefinirDisponibilidade(bool disponivel)
    {
        if (disponivel && !Ativo)
            throw new RegraNegocioException("Um usuário inativo não pode ficar disponível para atendimento.");

        DisponivelParaAtendimento = disponivel;
    }

    public void Desativar()
    {
        Ativo = false;
        DisponivelParaAtendimento = false;
    }
}
