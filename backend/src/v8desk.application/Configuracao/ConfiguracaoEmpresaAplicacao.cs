using v8desk.application.Abstractions;
using v8desk.application.Common;
using v8desk.domain.Entities;
using v8desk.domain.Enums;
using v8desk.domain.Exceptions;
using v8desk.domain.ValueObjects;

namespace v8desk.application.Configuracao;

public sealed class ConfiguracaoEmpresaAplicacao(IConfiguracaoRepository configuracoes, IAcessoRepository acessos,
    IAutorizacaoEmpresa autorizacao, IExecutorComandoIdempotente executor, IUnidadeTrabalho unidade,
    IEmpresaAtual empresa, IUsuarioAtual usuario)
{
    public async Task<ResultadoEmpresa> ExecutarAsync(ComandoEmpresa comando, string chave, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(comando);
        await ExigirAdministracaoAsync(cancellationToken);
        return await ComandoIdempotente.ExecutarAsync(executor, chave, comando, async ct =>
        {
            await ExigirAdministracaoAsync(ct);
            var atual = await configuracoes.ObterEmpresaAsync(ct) ?? throw new AcessoNegadoException("A empresa não está disponível.");
            if (atual.Id != empresa.EmpresaId) throw new AcessoNegadoException("A empresa não está disponível.");
            var registro = atual.Id;
            switch (comando)
            {
                case RenomearEmpresa c: atual.Renomear(c.Nome); break;
                case CriarSetor c:
                    var setor = atual.CriarSetor(c.Nome);
                    configuracoes.Adicionar(setor);
                    // O criador pode configurar SLA, filas e categorias do novo setor.
                    configuracoes.Adicionar(new VinculoSetor(await UsuarioAsync(usuario.UsuarioId, ct), setor.Id, PapelSetor.Gestor));
                    registro = setor.Id; break;
                case CriarUsuario c:
                    var novo = new Usuario(atual.Id, c.Nome);
                    configuracoes.Adicionar(novo); registro = novo.Id; break;
                case RenomearUsuario c: (await UsuarioAsync(c.UsuarioId, ct)).AtualizarIdentificacao(c.Nome); registro = c.UsuarioId; break;
                case DesativarUsuario c: (await UsuarioAsync(c.UsuarioId, ct)).Desativar(); registro = c.UsuarioId; break;
                case DefinirDisponibilidadeUsuario c: (await UsuarioAsync(c.UsuarioId, ct)).DefinirDisponibilidade(c.Disponivel); registro = c.UsuarioId; break;
                case ConfigurarLimiteSetoresAtendente c: atual.ConfigurarLimiteAtuacao(c.Limite); break;
                case ConfigurarCicloVidaEmpresa c: atual.ConfigurarCicloVida(c.Politica); break;
                case AlterarFusoHorario c: atual.Calendario.AlterarFusoHorario(c.FusoHorarioId); break;
                case DefinirExpediente c: atual.Calendario.DefinirExpediente(c.Dia, Intervalos(c.Intervalos)); break;
                case RegistrarExcecaoCalendario c: atual.Calendario.RegistrarExcecao(new(c.Data, c.Motivo, Intervalos(c.Intervalos))); break;
                case RemoverExcecaoCalendario c: atual.Calendario.RemoverExcecao(c.Data); break;
                default: throw new ValidacaoDominioException("comando_invalido", "comando", "Esta configuração não é reconhecida.");
            }
            await unidade.SalvarAsync(ct);
            return new ResultadoEmpresa(atual.Id, registro);
        }, cancellationToken);
    }
    private async Task ExigirAdministracaoAsync(CancellationToken ct)
    {
        await autorizacao.ExigirAdministracaoAsync(empresa.EmpresaId, usuario.UsuarioId, ct);
        var atual = await acessos.ObterUsuarioAsync(usuario.UsuarioId, ct);
        if (atual is null || atual.Id != usuario.UsuarioId || !atual.Ativo || atual.EmpresaId != empresa.EmpresaId)
            throw new AcessoNegadoException("Sua conta não está ativa nesta empresa.");
    }
    private async Task<Usuario> UsuarioAsync(Guid id, CancellationToken ct)
    {
        var u = await configuracoes.ObterUsuarioAsync(id, ct) ?? throw new RegraNegocioException("O usuário não está disponível.");
        if (u.EmpresaId != empresa.EmpresaId) throw new AcessoNegadoException("O usuário pertence a outra empresa.");
        return u;
    }
    private static IntervaloExpediente[] Intervalos(IReadOnlyList<IntervaloExpedienteEntrada> valores)
    {
        if (valores is null) throw new ValidacaoDominioException("intervalos_obrigatorios", "intervalos", "Informe os horários ou uma lista vazia para um dia sem expediente.");
        if (valores.Any(v => v is null)) throw new ValidacaoDominioException("intervalo_invalido", "intervalos", "Informe o início e o fim de cada horário.");
        return valores.Select(v => new IntervaloExpediente(v.Inicio, v.Fim)).ToArray();
    }
}
