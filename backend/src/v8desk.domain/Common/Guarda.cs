namespace v8desk.domain.Common;

internal static class Guarda
{
    public static string Texto(string? valor, string campo, int limite = 2000)
    {
        if (string.IsNullOrWhiteSpace(valor))
            throw new ValidacaoDominioException("campo_obrigatorio", campo, $"Informe {campo}.");

        var texto = valor.Trim();
        if (texto.Length > limite)
            throw new ValidacaoDominioException("texto_muito_longo", campo,
                $"O campo {campo} deve ter no máximo {limite} caracteres.");
        return texto;
    }

    public static Guid Identificador(Guid valor, string campo)
    {
        if (valor == Guid.Empty)
            throw new ValidacaoDominioException("campo_obrigatorio", campo, $"Informe {campo}.");

        return valor;
    }

    public static T Definido<T>(T valor, string campo) where T : struct, Enum
    {
        if (!Enum.IsDefined(valor))
            throw new ValidacaoDominioException("opcao_invalida", campo, $"Selecione uma opção válida para {campo}.");

        return valor;
    }

    public static DateTimeOffset Instante(DateTimeOffset valor, string campo)
    {
        if (valor == default)
            throw new ValidacaoDominioException("data_obrigatoria", campo, $"Informe {campo}.");
        return valor.ToUniversalTime();
    }
}
