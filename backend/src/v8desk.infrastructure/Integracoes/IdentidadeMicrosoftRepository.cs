using Npgsql;
using v8desk.application.Integracoes;

namespace v8desk.infrastructure.Integracoes;

// Somente descoberta de conexão e associação de identidade validada. Não lê chamados entre empresas.
public sealed class IdentidadeMicrosoftRepository(NpgsqlDataSource source) : IIdentidadeMicrosoftRepository
{
    public async Task<IdentidadeMicrosoft?> ResolverAsync(Guid tenantId, Guid apiClienteId, Guid objetoId, CancellationToken ct)
    {
        await using var cmd = source.CreateCommand("""
            SELECT v.empresa_id, v.usuario_id, v.administrador, v.ativo AND u.ativo AND i.microsoft_ativo
            FROM vinculos_microsoft v
            JOIN integracoes_empresa i ON i.empresa_id = v.empresa_id AND i.tenant_microsoft_id = v.tenant_id
            JOIN usuarios u ON u.empresa_id = v.empresa_id AND u.id = v.usuario_id
            WHERE v.tenant_id = @tenant AND i.api_microsoft_id = @api AND v.objeto_id = @objeto
            LIMIT 2
            """);
        cmd.Parameters.AddWithValue("tenant", tenantId); cmd.Parameters.AddWithValue("api", apiClienteId); cmd.Parameters.AddWithValue("objeto", objetoId);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        if (!await r.ReadAsync(ct)) return null;
        var resultado = new IdentidadeMicrosoft(r.GetGuid(0), r.GetGuid(1), r.GetBoolean(2), r.GetBoolean(3));
        if (await r.ReadAsync(ct)) return null; // Nunca resolver uma identidade ambígua.
        return resultado;
    }
    public async Task<AcessoMicrosoft?> AcessoAsync(Guid empresaId, CancellationToken ct)
    {
        await using var cmd = source.CreateCommand("SELECT tenant_microsoft_id, api_microsoft_id, microsoft ->> 'ClienteLoginId' FROM integracoes_empresa WHERE empresa_id = @empresa AND microsoft_ativo");
        cmd.Parameters.AddWithValue("empresa", empresaId);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        if (!await r.ReadAsync(ct) || r.IsDBNull(0) || r.IsDBNull(1) || r.IsDBNull(2)) return null;
        return new(r.GetGuid(0), r.GetGuid(1), Guid.Parse(r.GetString(2)), "https://login.microsoftonline.com/" + r.GetGuid(0) + "/v2.0",
            "api://" + r.GetGuid(1) + "/access_as_user");
    }
}
