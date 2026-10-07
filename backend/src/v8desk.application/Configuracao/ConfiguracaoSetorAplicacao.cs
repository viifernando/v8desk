using v8desk.application.Abstractions;
using v8desk.application.Common;
using v8desk.application.Seguranca;
using v8desk.domain.Entities;
using v8desk.domain.Enums;
using v8desk.domain.Exceptions;
using v8desk.domain.ValueObjects;

namespace v8desk.application.Configuracao;

public sealed class ConfiguracaoSetorAplicacao(IConfiguracaoRepository configuracoes, IAcessoRepository acessos,
    IExecutorComandoIdempotente executor, IUnidadeTrabalho unidade, IEmpresaAtual empresa, IUsuarioAtual usuario, TimeProvider relogio)
{
    public async Task<ResultadoConfiguracao> ExecutarAsync(ComandoSetor comando, string chave, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(comando);
        await ExigirGestaoAsync(comando.SetorId, cancellationToken);
        return await ComandoIdempotente.ExecutarAsync(executor, chave, comando, async ct =>
        {
            await configuracoes.BloquearSetorAsync(comando.SetorId, ct);
            await ExigirGestaoAsync(comando.SetorId, ct);
            var setor = await configuracoes.ObterSetorAsync(comando.SetorId, ct)
                ?? throw new AcessoNegadoException("O setor não está disponível para sua conta.");
            if (setor.EmpresaId != empresa.EmpresaId || !setor.Ativo) throw new AcessoNegadoException("O setor não está ativo nesta empresa.");
            var registro = setor.Id;
            switch (comando)
            {
                case RenomearSetor c: setor.Renomear(c.Nome); break;
                case DesativarSetor: setor.Desativar(); break;
                case CriarFila c: registro = setor.CriarFila(c.Nome).Id; break;
                case RenomearFila c: setor.RenomearFila(Fila(setor, c.FilaId), c.Nome); break;
                case DesativarFila c: Fila(setor, c.FilaId).Desativar(); break;
                case CriarCategoria c: registro = setor.CriarCategoria(c.Nome, c.PaiId is { } pai ? Categoria(setor, pai) : null).Id; break;
                case RenomearCategoria c: setor.RenomearCategoria(Categoria(setor, c.CategoriaId), c.Nome); break;
                case MoverCategoria c: setor.MoverCategoria(Categoria(setor, c.CategoriaId), c.PaiId is { } novoPai ? Categoria(setor, novoPai) : null); break;
                case DesativarCategoria c: Categoria(setor, c.CategoriaId).Desativar(); break;
                case ReativarCategoria c: setor.ReativarCategoria(Categoria(setor, c.CategoriaId)); break;
                case ConfigurarRecebimentoCategoria c: setor.ConfigurarRecebimentoCategoria(Categoria(setor, c.CategoriaId), c.PermitirComSubcategorias); break;
                case ConfigurarDestinoCategoria c: Categoria(setor, c.CategoriaId).ConfigurarDestino(c.FilaId is { } fila ? Fila(setor, fila) : null); break;
                case ConfigurarVisibilidadeCategoria c: Categoria(setor, c.CategoriaId).DefinirVisibilidadePadrao(c.Visibilidade); break;
                case PreQualificarCategoria c: Categoria(setor, c.CategoriaId).PreQualificar(await UsuarioAsync(c.UsuarioId, ct), relogio.GetUtcNow()); break;
                case RemoverPreQualificacao c: Categoria(setor, c.CategoriaId).RemoverPreQualificacao(c.UsuarioId); break;
                case DefinirMetaSla c: setor.Sla.DefinirMeta(c.Prioridade, c.Tipo, new HorasUteis(c.HorasUteis)); break;
                case ConfigurarCicloVidaSetor c: setor.ConfigurarCicloVida(c.Politica); break;
                case ConfigurarAcessoRestritoFila c: Fila(setor, c.FilaId).ConfigurarAcessoRestrito(c.Regra); break;
                case AutorizarRestritos c: Fila(setor, c.FilaId).AutorizarAcessoRestrito(c.UsuarioId); break;
                case RevogarRestritos c: Fila(setor, c.FilaId).RevogarAcessoRestrito(c.UsuarioId); break;
                case AdicionarMembroFila c: Fila(setor, c.FilaId).AdicionarMembro(await VinculoAsync(c.VinculoId, setor, ct)); break;
                case RemoverMembroFila c: Fila(setor, c.FilaId).RemoverMembro(c.UsuarioId); break;
                case CriarVinculoSetor c:
                    var novoUsuario = await UsuarioAsync(c.UsuarioId, ct);
                    var dados = await acessos.ObterDadosAsync(c.UsuarioId, [], ct) ?? throw new RegraNegocioException("O usuário não está disponível.");
                    if (dados.Vinculos.Any(v => v.SetorId == setor.Id && v.Ativo))
                        throw new RegraNegocioException("O usuário já está vinculado ao setor. Altere os papéis do vínculo existente.");
                    if (c.Papel == PapelSetor.Atendente) await ValidarLimiteAsync(novoUsuario, ct);
                    var novo = new VinculoSetor(novoUsuario, setor.Id, c.Papel);
                    configuracoes.Adicionar(novo); registro = novo.Id; break;
                case ConcederPapelSetor c:
                    var vinculo = await VinculoAsync(c.VinculoId, setor, ct);
                    if (c.Papel == PapelSetor.Atendente && !vinculo.PossuiPapel(PapelSetor.Atendente))
                        await ValidarLimiteAsync(vinculo.Usuario, ct);
                    vinculo.ConcederPapel(c.Papel); break;
                case RevogarPapelSetor c: (await VinculoAsync(c.VinculoId, setor, ct)).RevogarPapel(c.Papel); break;
                case DesativarVinculoSetor c: (await VinculoAsync(c.VinculoId, setor, ct)).Desativar(); break;
                default: throw new ValidacaoDominioException("comando_invalido", "comando", "Esta configuração não é reconhecida.");
            }
            await unidade.SalvarAsync(ct);
            return new ResultadoConfiguracao(setor.Id, registro);
        }, cancellationToken);
    }
    private async Task ExigirGestaoAsync(Guid setorId, CancellationToken ct)
    {
        var dados = await acessos.ObterDadosAsync(usuario.UsuarioId, [], ct) ?? throw new AcessoNegadoException("Sua conta não está disponível.");
        if (dados.Usuario.Id != usuario.UsuarioId) throw new AcessoNegadoException("Sua conta não está disponível.");
        new AutorizacaoChamado(empresa.EmpresaId, dados).ExigirGestaoSetor(usuario.UsuarioId, setorId);
    }
    private static Categoria Categoria(Setor s, Guid id) => s.Categorias.SingleOrDefault(c => c.Id == id)
        ?? throw new RegraNegocioException("Escolha uma categoria deste setor.");
    private static Fila Fila(Setor s, Guid id) => s.Filas.SingleOrDefault(f => f.Id == id)
        ?? throw new RegraNegocioException("Escolha uma fila deste setor.");
    private async Task<Usuario> UsuarioAsync(Guid id, CancellationToken ct)
    {
        var u = await configuracoes.ObterUsuarioAsync(id, ct) ?? throw new RegraNegocioException("O usuário não está disponível.");
        if (u.EmpresaId != empresa.EmpresaId || !u.Ativo) throw new RegraNegocioException("Escolha um usuário ativo desta empresa.");
        return u;
    }
    private async Task<VinculoSetor> VinculoAsync(Guid id, Setor setor, CancellationToken ct)
    {
        var v = await configuracoes.ObterVinculoAsync(id, ct) ?? throw new RegraNegocioException("O vínculo não está disponível.");
        if (v.SetorId != setor.Id || v.Usuario.EmpresaId != empresa.EmpresaId) throw new AcessoNegadoException("O vínculo pertence a outro setor ou empresa.");
        return v;
    }
    private async Task ValidarLimiteAsync(Usuario u, CancellationToken ct)
    {
        await configuracoes.BloquearVinculosUsuarioAsync(u.Id, ct);
        var dados = await acessos.ObterDadosAsync(u.Id, [], ct) ?? throw new RegraNegocioException("O usuário não está disponível.");
        var atual = await configuracoes.ObterEmpresaAsync(ct) ?? throw new AcessoNegadoException("A empresa não está disponível.");
        if (atual.Id != empresa.EmpresaId) throw new AcessoNegadoException("A empresa não está disponível.");
        atual.ValidarNovoVinculoAtendimento(u, dados.Vinculos.Where(v => v.PossuiPapel(PapelSetor.Atendente)).Select(v => v.SetorId).Distinct().Count());
    }
}
