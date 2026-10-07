using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using v8desk.application.Abstractions;
using v8desk.domain.Exceptions;

namespace v8desk.infrastructure.Persistence;

public sealed class ExecutorComandoIdempotente(V8DeskDbContext db, IUsuarioAtual usuario) : IExecutorComandoIdempotente
{
    public async Task<string> ExecutarAsync(string chave, string conteudoCanonico,
        Func<CancellationToken, Task<string>> comando, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(chave) || chave.Length > 200)
            throw new ValidacaoDominioException("chave_invalida", "chave", "Informe uma chave de operação de até 200 caracteres.");
        ArgumentNullException.ThrowIfNull(conteudoCanonico);
        ArgumentNullException.ThrowIfNull(comando);
        if (db.ChangeTracker.Entries().Any() || db.Database.CurrentTransaction is not null)
            throw new InvalidOperationException("Execute o comando em uma unidade de trabalho nova.");
        var usuarioId = usuario.UsuarioId;
        if (usuarioId == Guid.Empty) throw new AcessoNegadoException("Identifique o usuário da operação.");
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(conteudoCanonico)));
        var trava = $"comando:{db.EmpresaId}:{usuarioId}:{chave}";
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
            if (!await db.Usuarios.AnyAsync(x => x.Id == usuarioId && x.Ativo, cancellationToken))
                throw new AcessoNegadoException("Seu usuário não está ativo nesta empresa.");
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({trava}, 0))", cancellationToken);
            var anterior = await db.Set<ComandoExecutado>().AsNoTracking()
                .SingleOrDefaultAsync(x => x.UsuarioId == usuarioId && x.Chave == chave, cancellationToken);
            if (anterior is not null)
            {
                if (anterior.Hash != hash) throw new RegraNegocioException("Esta chave já foi utilizada com outros dados. Use uma nova chave de operação.");
                await tx.CommitAsync(cancellationToken);
                return anterior.Resultado;
            }
            var resultado = await comando(cancellationToken);
            db.Add(new ComandoExecutado(db.EmpresaId, usuarioId, chave, hash, resultado));
            await db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
            return resultado;
        });
    }
}
