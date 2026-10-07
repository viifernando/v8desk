namespace v8desk.domain.Exceptions;

public sealed class ValidacaoDominioException : RegraNegocioException
{
    public string Codigo { get; }
    public string Campo { get; }

    public ValidacaoDominioException(string codigo, string campo, string mensagem) : base(mensagem)
    {
        Codigo = codigo;
        Campo = campo;
    }
}
