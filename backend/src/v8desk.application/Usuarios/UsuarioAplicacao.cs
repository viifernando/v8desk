using v8desk.application.Abstractions;
using v8desk.application.Common;
using v8desk.domain.Exceptions;

namespace v8desk.application.Usuarios;

public sealed record DefinirMinhaDisponibilidade(bool Disponivel);
public sealed record ResultadoDisponibilidade(Guid UsuarioId, bool Disponivel);

public sealed class UsuarioAplicacao(IConfiguracaoRepository configuracoes, IAcessoRepository acessos,
    IExecutorComandoIdempotente executor, IUnidadeTrabalho unidade, IEmpresaAtual empresa, IUsuarioAtual usuario)
{
    public async Task<ResultadoDisponibilidade> DefinirDisponibilidadeAsync(DefinirMinhaDisponibilidade comando,
        string chave, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(comando);
        await ExigirUsuarioAsync(cancellationToken);
        return await ComandoIdempotente.ExecutarAsync(executor, chave, comando, async ct =>
        {
            await ExigirUsuarioAsync(ct);
            var atual = await configuracoes.ObterUsuarioAsync(usuario.UsuarioId, ct)
                ?? throw new AcessoNegadoException("Sua conta não está disponível.");
            if (atual.Id != usuario.UsuarioId || atual.EmpresaId != empresa.EmpresaId || !atual.Ativo)
                throw new AcessoNegadoException("Sua conta não está ativa nesta empresa.");
            atual.DefinirDisponibilidade(comando.Disponivel);
            await unidade.SalvarAsync(ct);
            return new ResultadoDisponibilidade(atual.Id, atual.DisponivelParaAtendimento);
        }, cancellationToken);
    }
    private async Task ExigirUsuarioAsync(CancellationToken ct)
    {
        var atual = await acessos.ObterUsuarioAsync(usuario.UsuarioId, ct);
        if (atual is null || atual.Id != usuario.UsuarioId || atual.EmpresaId != empresa.EmpresaId || !atual.Ativo)
            throw new AcessoNegadoException("Sua conta não está ativa nesta empresa.");
    }
}
