using Npgsql;
using v8desk.domain.Exceptions;

namespace v8desk.infrastructure.Persistence;

public static class ErrosPersistencia
{
    public static RegraNegocioException? Traduzir(PostgresException erro) => erro.SqlState switch
    {
        PostgresErrorCodes.UniqueViolation => new("Já existe um registro com estes dados. Atualize a tela e confira os valores informados."),
        PostgresErrorCodes.ForeignKeyViolation => new("Um registro relacionado não está disponível ou pertence a outro setor. Atualize a tela e selecione novamente."),
        PostgresErrorCodes.CheckViolation => new("Os dados não atendem às regras deste registro. Confira os valores e tente novamente."),
        _ => null
    };
}
