using v8desk.application.Abstractions;
using v8desk.domain.Exceptions;

namespace v8desk.application.Chamados;

// Exclusivo do worker: não expor como comando do solicitante/atendente na API.
public sealed class EncerramentoAutomaticoAplicacao(IChamadoRepository chamados, IUnidadeTrabalho unidade,
    IEmpresaAtual empresa, TimeProvider relogio)
{
    public async Task<bool> EncerrarAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var chamado = await chamados.ObterParaAtualizacaoAsync(id, cancellationToken);
        if (chamado is null) return false;
        if (chamado.EmpresaId != empresa.EmpresaId) throw new AcessoNegadoException("O chamado pertence a outra empresa.");
        var versao = chamado.Versao;
        chamado.EncerrarPorPrazo(relogio.GetUtcNow());
        if (chamado.Versao == versao) return false;
        await unidade.SalvarAsync(cancellationToken);
        return true;
    }
}
