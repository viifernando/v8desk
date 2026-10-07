namespace v8desk.domain.Common;

internal static class Guarda
{
    public static string Texto(string? valor, string campo)
    {
        if (string.IsNullOrWhiteSpace(valor))
            throw new RegraNegocioException($"Informe {campo}.");

        return valor.Trim();
    }

    public static Guid Identificador(Guid valor, string campo)
    {
        if (valor == Guid.Empty)
            throw new RegraNegocioException($"Informe {campo}.");

        return valor;
    }

    public static T Definido<T>(T valor, string campo) where T : struct, Enum
    {
        if (!Enum.IsDefined(valor))
            throw new RegraNegocioException($"Valor inválido para {campo}.");

        return valor;
    }
}
