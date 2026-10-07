using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using v8desk.domain.Entities;
using v8desk.domain.Enums;
using v8desk.domain.ValueObjects;

namespace v8desk.infrastructure.Persistence;

internal static class Mapeamentos
{
    public static void Configurar(ModelBuilder m)
    {
        foreach (var tipo in typeof(HorasUteis).Assembly.GetTypes().Where(t => t.Namespace == "v8desk.domain.ValueObjects"))
            m.Ignore(tipo);
        m.Ignore<CalendarioEmpresa>();
        m.Ignore<Avaliacao>();
        m.Ignore<Anexo>();
        var empresa = m.Entity<Empresa>();
        empresa.ToTable("empresas");
        empresa.HasKey(x => x.Id);
        empresa.Property(x => x.Id).ValueGeneratedNever();
        empresa.Property(x => x.Nome).HasMaxLength(200);
        JsonPersistencia.Mapear(empresa.Property(x => x.Calendario));
        JsonPersistencia.Mapear(empresa.Property(x => x.PadraoCicloVida));
        empresa.Property<uint>("xmin").IsRowVersion();

        var usuario = Base<Usuario>(m, "usuarios");
        usuario.Property(x => x.Nome).HasMaxLength(200);

        var setor = Base<Setor>(m, "setores");
        setor.Property(x => x.Nome).HasMaxLength(200);
        JsonPersistencia.Mapear(setor.Property(x => x.CicloVidaEspecifico));
        setor.Ignore(x => x.FilaGeral);
        setor.HasMany(x => x.Filas).WithOne().HasForeignKey("EmpresaId", "SetorId").OnDelete(DeleteBehavior.Restrict);
        setor.Navigation(x => x.Filas).HasField("_filas").UsePropertyAccessMode(PropertyAccessMode.Field);
        setor.HasMany(x => x.Categorias).WithOne().HasForeignKey("EmpresaId", "SetorId").OnDelete(DeleteBehavior.Restrict);
        setor.Navigation(x => x.Categorias).HasField("_categorias").UsePropertyAccessMode(PropertyAccessMode.Field);
        setor.Property<Guid>("PoliticaSlaId");
        setor.HasOne(x => x.Sla).WithMany().HasForeignKey("EmpresaId", "PoliticaSlaId").OnDelete(DeleteBehavior.Restrict);

        var politica = Base<PoliticaSla>(m, "politicas_sla");
        politica.Ignore(x => x.Metas);
        JsonPersistencia.Mapear(politica.Property<List<MetaSla>>("_metas").HasColumnName("metas"));

        var categoria = Base<Categoria>(m, "categorias");
        categoria.Property(x => x.Nome).HasMaxLength(200);
        categoria.Ignore(x => x.CategoriaPaiId);
        categoria.Ignore(x => x.PodeReceberChamado);
        categoria.Ignore(x => x.PreQualificacoes);
        categoria.Property<Guid?>("PaiId");
        categoria.HasOne(x => x.Pai).WithMany(x => x.Subcategorias)
            .HasForeignKey("EmpresaId", "PaiId").OnDelete(DeleteBehavior.Restrict);
        categoria.Navigation(x => x.Subcategorias).HasField("_filhas").UsePropertyAccessMode(PropertyAccessMode.Field);
        categoria.HasOne<Fila>().WithMany().HasForeignKey("EmpresaId", "FilaDestinoId").OnDelete(DeleteBehavior.Restrict);
        JsonPersistencia.Mapear(categoria.Property<List<PreQualificacao>>("_preQualificacoes").HasColumnName("pre_qualificacoes"));
        categoria.HasIndex("EmpresaId", "SetorId", "PaiId", "Nome").IsUnique().AreNullsDistinct(false)
            .HasFilter("ativa = TRUE");

        var fila = Base<Fila>(m, "filas");
        fila.Property(x => x.Nome).HasMaxLength(200);
        fila.HasMany(x => x.Membros).WithOne().HasForeignKey("EmpresaId", "FilaId").OnDelete(DeleteBehavior.Restrict);
        fila.Navigation(x => x.Membros).HasField("_membros").UsePropertyAccessMode(PropertyAccessMode.Field);
        fila.HasIndex("EmpresaId", "SetorId", "Nome").IsUnique().HasFilter("ativa = TRUE");

        var vinculo = Base<VinculoSetor>(m, "vinculos_setor");
        vinculo.Ignore(x => x.UsuarioId);
        vinculo.Ignore(x => x.Papeis);
        vinculo.Property<Guid>("UsuarioPersistidoId").HasColumnName("usuario_id");
        vinculo.HasOne(x => x.Usuario).WithMany().HasForeignKey("EmpresaId", "UsuarioPersistidoId").OnDelete(DeleteBehavior.Restrict);
        vinculo.HasOne<Setor>().WithMany().HasForeignKey("EmpresaId", "SetorId").OnDelete(DeleteBehavior.Restrict);
        JsonPersistencia.Mapear(vinculo.Property<HashSet<PapelSetor>>("_papeis").HasColumnName("papeis"));
        vinculo.HasIndex("EmpresaId", "UsuarioPersistidoId", "SetorId").IsUnique().HasFilter("ativo = TRUE");

        var membro = m.Entity<MembroFila>();
        membro.ToTable("membros_fila");
        membro.Property<Guid>("EmpresaId").ValueGeneratedNever();
        membro.Property<Guid>("FilaId");
        membro.HasKey("EmpresaId", "FilaId", "UsuarioId");
        membro.Ignore(x => x.VinculoSetorId);
        membro.Ignore(x => x.Habilitado);
        membro.Property<Guid>("VinculoId");
        membro.HasOne(x => x.Vinculo).WithMany().HasForeignKey("EmpresaId", "VinculoId").OnDelete(DeleteBehavior.Restrict);
        membro.Navigation(x => x.Vinculo).AutoInclude();
        vinculo.Navigation(x => x.Usuario).AutoInclude();
        membro.Property<uint>("xmin").IsRowVersion();

        var chamado = Base<Chamado>(m, "chamados");
        chamado.Property(x => x.Titulo).HasMaxLength(200);
        chamado.Property(x => x.DescricaoOriginal).HasMaxLength(20000);
        chamado.Property(x => x.Versao).IsConcurrencyToken();
        chamado.Ignore(x => x.CicloAtual);
        chamado.Ignore(x => x.CategoriaId);
        chamado.Ignore(x => x.SetorAtualId);
        chamado.Ignore(x => x.FilaAtualId);
        chamado.Ignore(x => x.ResponsavelId);
        JsonPersistencia.Mapear(chamado.Property(x => x.SetorOrigemNaAbertura));
        JsonPersistencia.Mapear(chamado.Property(x => x.CategoriaAtual));
        JsonPersistencia.Mapear(chamado.Property(x => x.ContextoAtual));
        JsonPersistencia.Mapear(chamado.Property(x => x.MetasSla));
        JsonPersistencia.Mapear(chamado.Property(x => x.CicloVidaAplicado));
        ContextoColunas(chamado, "contexto_atual");
        chamado.Property<Guid>("SetorOrigemConsultaId").HasColumnName("setor_origem_id")
            .HasComputedColumnSql("(setor_origem_na_abertura ->> 'Id')::uuid", stored: true);
        chamado.HasOne<Usuario>().WithMany().HasForeignKey("EmpresaId", "SolicitanteId").OnDelete(DeleteBehavior.Restrict);
        chamado.HasMany(x => x.Eventos).WithOne().HasForeignKey("EmpresaId", "ChamadoId").OnDelete(DeleteBehavior.Restrict);
        chamado.Navigation(x => x.Eventos).HasField("_eventos").UsePropertyAccessMode(PropertyAccessMode.Field);
        chamado.HasMany(x => x.Mensagens).WithOne().HasForeignKey("EmpresaId", "ChamadoId").OnDelete(DeleteBehavior.Restrict);
        chamado.Navigation(x => x.Mensagens).HasField("_mensagens").UsePropertyAccessMode(PropertyAccessMode.Field);
        chamado.HasMany(x => x.Ciclos).WithOne().HasForeignKey("EmpresaId", "ChamadoId").OnDelete(DeleteBehavior.Restrict);
        chamado.Navigation(x => x.Ciclos).HasField("_ciclos").UsePropertyAccessMode(PropertyAccessMode.Field);
        chamado.HasIndex("EmpresaId", "FilaConsultaId", "Status", "AbertoEm", "Id")
            .HasFilter("status NOT IN ('Encerrado', 'Cancelado', 'Resolvido')");
        chamado.HasIndex("EmpresaId", "ResponsavelConsultaId", "Status", "AbertoEm", "Id");
        chamado.HasIndex("EmpresaId", "Status", "LimiteValidacao")
            .HasFilter("status = 'Resolvido'");

        var evento = Base<EventoChamado>(m, "eventos_chamado");
        evento.Property(x => x.Motivo).HasMaxLength(2000);
        JsonPersistencia.Mapear(evento.Property(x => x.Antes));
        JsonPersistencia.Mapear(evento.Property(x => x.Depois));
        evento.HasIndex("EmpresaId", "ChamadoId", "Sequencia").IsUnique();

        var mensagem = Base<Mensagem>(m, "mensagens");
        mensagem.Property(x => x.TextoOriginal).HasMaxLength(20000);
        mensagem.Ignore(x => x.TextoAtual);
        mensagem.Ignore(x => x.Correcoes);
        JsonPersistencia.Mapear(mensagem.Property<List<CorrecaoMensagem>>("_correcoes").HasColumnName("correcoes"));
        mensagem.HasIndex("EmpresaId", "ChamadoId", "CriadaEm", "Id");
        mensagem.Ignore(x => x.Anexos);
        // Metadados pertencem à mensagem; conteúdo binário fica fora do banco.
        JsonPersistencia.Mapear(mensagem.Property<List<Anexo>>("_anexos").HasColumnName("anexos"));

        var ciclo = Base<CicloAtendimento>(m, "ciclos_atendimento");
        ciclo.Ignore(x => x.Encerrado);
        ciclo.Ignore(x => x.PeriodoAtual);
        ciclo.Ignore(x => x.AguardandoRespostaEquipe);
        ciclo.Ignore(x => x.SolucoesRejeitadas);
        JsonPersistencia.Mapear(ciclo.Property(x => x.Solucao));
        JsonPersistencia.Mapear(ciclo.Property(x => x.Avaliacao));
        JsonPersistencia.Mapear(ciclo.Property<List<SolucaoRejeitada>>("_solucoesRejeitadas").HasColumnName("solucoes_rejeitadas"));
        ciclo.HasMany(x => x.Periodos).WithOne().HasForeignKey("EmpresaId", "CicloAtendimentoId").OnDelete(DeleteBehavior.Restrict);
        ciclo.Navigation(x => x.Periodos).HasField("_periodos").UsePropertyAccessMode(PropertyAccessMode.Field);
        ciclo.HasMany(x => x.CiclosSla).WithOne().HasForeignKey("EmpresaId", "CicloAtendimentoId").OnDelete(DeleteBehavior.Restrict);
        ciclo.Navigation(x => x.CiclosSla).HasField("_ciclosSla").UsePropertyAccessMode(PropertyAccessMode.Field);
        ciclo.HasIndex("EmpresaId", "ChamadoId", "Numero").IsUnique();

        var periodo = Base<PeriodoEtapa>(m, "periodos_etapa");
        periodo.Ignore(x => x.Aberto);
        JsonPersistencia.Mapear(periodo.Property(x => x.Contexto));
        JsonPersistencia.Mapear(periodo.Property(x => x.CalendarioAplicado));
        ContextoColunas(periodo, "contexto");
        periodo.HasIndex("EmpresaId", "SetorConsultaId", "Status", "Entrada");
        periodo.HasIndex("EmpresaId", "CicloAtendimentoId").HasFilter("saida IS NULL");

        var sla = Base<CicloSla>(m, "ciclos_sla");
        sla.Ignore(x => x.Ativo);
        sla.Ignore(x => x.Pausado);
        sla.Ignore(x => x.VersaoCalendarioAplicada);
        sla.Ignore(x => x.Pausas);
        sla.Ignore(x => x.RevisoesMeta);
        JsonPersistencia.Mapear(sla.Property(x => x.CalendarioAplicado));
        sla.Property(x => x.MetaAplicada).HasConversion(v => v.Valor, v => new HorasUteis(v)).HasColumnType("numeric");
        JsonPersistencia.Mapear(sla.Property<List<PausaSla>>("_pausas").HasColumnName("pausas"));
        JsonPersistencia.Mapear(sla.Property<List<RevisaoMetaSla>>("_revisoesMeta").HasColumnName("revisoes_meta"));
        sla.HasIndex("EmpresaId", "SetorId", "Tipo", "IniciadoEm");
        sla.HasIndex("EmpresaId", "CicloAtendimentoId", "Tipo").HasFilter("finalizado_em IS NULL");

        var outbox = Base<OutboxMensagem>(m, "outbox");
        outbox.Property(x => x.Payload).HasColumnType("jsonb");
        outbox.Property(x => x.Tipo).HasMaxLength(100);
        outbox.HasIndex("EmpresaId", "CriadaEm", "Id").HasFilter("processada_em IS NULL");

        foreach (var entidade in m.Model.GetEntityTypes())
        {
            foreach (var propriedade in entidade.GetProperties())
            {
                if (propriedade.GetColumnName() == propriedade.Name)
                    propriedade.SetColumnName(Snake(propriedade.Name.TrimStart('_')));
                var tipo = Nullable.GetUnderlyingType(propriedade.ClrType) ?? propriedade.ClrType;
                if (tipo.IsEnum) m.Entity(entidade.ClrType).Property(propriedade.Name).HasConversion<string>().HasMaxLength(60);
                if (tipo == typeof(DateTimeOffset))
                    propriedade.SetValueConverter(new ValueConverter<DateTimeOffset, DateTimeOffset>(v => v.ToUniversalTime(), v => v));
            }
        }
    }

    private static EntityTypeBuilder<T> Base<T>(ModelBuilder m, string tabela) where T : class
    {
        var b = m.Entity<T>();
        b.ToTable(tabela);
        foreach (var propriedade in typeof(T).GetProperties())
        {
            var tipo = Nullable.GetUnderlyingType(propriedade.PropertyType) ?? propriedade.PropertyType;
            if (tipo.IsEnum || tipo == typeof(Guid) || tipo == typeof(string) || tipo == typeof(bool) ||
                tipo == typeof(int) || tipo == typeof(long) || tipo == typeof(decimal) || tipo == typeof(DateTimeOffset))
                b.Property(propriedade.PropertyType, propriedade.Name);
        }
        b.Property<Guid>("EmpresaId").ValueGeneratedNever();
        b.Property<Guid>("Id").ValueGeneratedNever();
        b.HasKey("EmpresaId", "Id");
        b.Property<uint>("xmin").IsRowVersion();
        b.HasOne<Empresa>().WithMany().HasForeignKey("EmpresaId").OnDelete(DeleteBehavior.Restrict);
        return b;
    }

    private static void ContextoColunas<T>(EntityTypeBuilder<T> b, string coluna) where T : class
    {
        foreach (var (nome, membro, nomeColuna) in new[]
        {
            ("FilaConsultaId", "Fila", "fila_atual_id"),
            ("SetorConsultaId", "Setor", "setor_atual_id"),
            ("ResponsavelConsultaId", "Responsavel", "responsavel_atual_id")
        })
        {
            var sql = $"({coluna} -> '{membro}' ->> 'Id')::uuid";
            if (membro == "Responsavel") b.Property<Guid?>(nome).HasColumnName(nomeColuna).HasComputedColumnSql(sql, stored: true);
            else b.Property<Guid>(nome).HasColumnName(nomeColuna).HasComputedColumnSql(sql, stored: true);
        }
    }

    private static string Snake(string nome) => System.Text.RegularExpressions.Regex.Replace(nome, "(?<!^)([A-Z])", "_$1").ToLowerInvariant();
}
