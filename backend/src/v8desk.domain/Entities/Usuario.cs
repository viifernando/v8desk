namespace v8desk.domain.Entities;

public sealed class Usuario
{
    public Guid Id { get; }
    public Guid EmpresaId { get; }
    public string Nome { get; private set; }
    public bool Ativo { get; private set; }
    public bool DisponivelParaAtendimento { get; private set; }

    public Usuario(Guid empresaId, string nome)
    {
        Id = Guid.CreateVersion7();
        EmpresaId = Guarda.Identificador(empresaId, "a empresa do usuário");
        Nome = Guarda.Texto(nome, "o nome do usuário");
        Ativo = true;
        DisponivelParaAtendimento = true;
    }

    public void AtualizarIdentificacao(string nome)
    {
        Nome = Guarda.Texto(nome, "o nome do usuário");
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
