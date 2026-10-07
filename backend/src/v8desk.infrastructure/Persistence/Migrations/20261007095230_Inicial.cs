using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace v8desk.infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "empresas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    calendario = table.Column<string>(type: "jsonb", nullable: false),
                    padrao_ciclo_vida = table.Column<string>(type: "jsonb", nullable: false),
                    limite_setores_por_atendente = table.Column<int>(type: "integer", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_empresas", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "outbox",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    payload = table.Column<string>(type: "jsonb", nullable: false),
                    criada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    processada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_outbox", x => new { x.empresa_id, x.id });
                    table.ForeignKey(
                        name: "FK_outbox_empresas_empresa_id",
                        column: x => x.empresa_id,
                        principalTable: "empresas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "politicas_sla",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    versao = table.Column<long>(type: "bigint", nullable: false),
                    percentual_alerta = table.Column<decimal>(type: "numeric", nullable: false),
                    metas = table.Column<string>(type: "jsonb", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_politicas_sla", x => new { x.empresa_id, x.id });
                    table.ForeignKey(
                        name: "FK_politicas_sla_empresas_empresa_id",
                        column: x => x.empresa_id,
                        principalTable: "empresas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "usuarios",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ativo = table.Column<bool>(type: "boolean", nullable: false),
                    disponivel_para_atendimento = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_usuarios", x => new { x.empresa_id, x.id });
                    table.ForeignKey(
                        name: "FK_usuarios_empresas_empresa_id",
                        column: x => x.empresa_id,
                        principalTable: "empresas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "setores",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ativo = table.Column<bool>(type: "boolean", nullable: false),
                    fila_geral_id = table.Column<Guid>(type: "uuid", nullable: false),
                    politica_sla_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ciclo_vida_especifico = table.Column<string>(type: "jsonb", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_setores", x => new { x.empresa_id, x.id });
                    table.ForeignKey(
                        name: "FK_setores_empresas_empresa_id",
                        column: x => x.empresa_id,
                        principalTable: "empresas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_setores_politicas_sla_empresa_id_politica_sla_id",
                        columns: x => new { x.empresa_id, x.politica_sla_id },
                        principalTable: "politicas_sla",
                        principalColumns: new[] { "empresa_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "chamados",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    solicitante_id = table.Column<Guid>(type: "uuid", nullable: false),
                    setor_origem_na_abertura = table.Column<string>(type: "jsonb", nullable: false),
                    titulo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    descricao_original = table.Column<string>(type: "character varying(20000)", maxLength: 20000, nullable: false),
                    status = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    prioridade = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    visibilidade = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    categoria_atual = table.Column<string>(type: "jsonb", nullable: false),
                    contexto_atual = table.Column<string>(type: "jsonb", nullable: false),
                    metas_sla = table.Column<string>(type: "jsonb", nullable: false),
                    ciclo_vida_aplicado = table.Column<string>(type: "jsonb", nullable: false),
                    aberto_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    atualizado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    limite_validacao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    lembrete_validacao_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    limite_reabertura = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    versao = table.Column<long>(type: "bigint", nullable: false),
                    fila_atual_id = table.Column<Guid>(type: "uuid", nullable: false, computedColumnSql: "(contexto_atual -> 'Fila' ->> 'Id')::uuid", stored: true),
                    responsavel_atual_id = table.Column<Guid>(type: "uuid", nullable: true, computedColumnSql: "(contexto_atual -> 'Responsavel' ->> 'Id')::uuid", stored: true),
                    setor_atual_id = table.Column<Guid>(type: "uuid", nullable: false, computedColumnSql: "(contexto_atual -> 'Setor' ->> 'Id')::uuid", stored: true),
                    setor_origem_id = table.Column<Guid>(type: "uuid", nullable: false, computedColumnSql: "(setor_origem_na_abertura ->> 'Id')::uuid", stored: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chamados", x => new { x.empresa_id, x.id });
                    table.ForeignKey(
                        name: "FK_chamados_empresas_empresa_id",
                        column: x => x.empresa_id,
                        principalTable: "empresas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_chamados_usuarios_empresa_id_solicitante_id",
                        columns: x => new { x.empresa_id, x.solicitante_id },
                        principalTable: "usuarios",
                        principalColumns: new[] { "empresa_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "filas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    setor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    geral = table.Column<bool>(type: "boolean", nullable: false),
                    ativa = table.Column<bool>(type: "boolean", nullable: false),
                    regra_acesso_restrito = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_filas", x => new { x.empresa_id, x.id });
                    table.ForeignKey(
                        name: "FK_filas_empresas_empresa_id",
                        column: x => x.empresa_id,
                        principalTable: "empresas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_filas_setores_empresa_id_setor_id",
                        columns: x => new { x.empresa_id, x.setor_id },
                        principalTable: "setores",
                        principalColumns: new[] { "empresa_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "vinculos_setor",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    setor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ativo = table.Column<bool>(type: "boolean", nullable: false),
                    papeis = table.Column<string>(type: "jsonb", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vinculos_setor", x => new { x.empresa_id, x.id });
                    table.ForeignKey(
                        name: "FK_vinculos_setor_empresas_empresa_id",
                        column: x => x.empresa_id,
                        principalTable: "empresas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_vinculos_setor_setores_empresa_id_setor_id",
                        columns: x => new { x.empresa_id, x.setor_id },
                        principalTable: "setores",
                        principalColumns: new[] { "empresa_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_vinculos_setor_usuarios_empresa_id_usuario_id",
                        columns: x => new { x.empresa_id, x.usuario_id },
                        principalTable: "usuarios",
                        principalColumns: new[] { "empresa_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ciclos_atendimento",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    numero = table.Column<int>(type: "integer", nullable: false),
                    iniciado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    encerrado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    motivo_de_encerramento = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    solucao = table.Column<string>(type: "jsonb", nullable: true),
                    avaliacao = table.Column<string>(type: "jsonb", nullable: true),
                    limite_avaliacao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    chamado_id = table.Column<Guid>(type: "uuid", nullable: true),
                    solucoes_rejeitadas = table.Column<string>(type: "jsonb", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ciclos_atendimento", x => new { x.empresa_id, x.id });
                    table.ForeignKey(
                        name: "FK_ciclos_atendimento_chamados_empresa_id_chamado_id",
                        columns: x => new { x.empresa_id, x.chamado_id },
                        principalTable: "chamados",
                        principalColumns: new[] { "empresa_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ciclos_atendimento_empresas_empresa_id",
                        column: x => x.empresa_id,
                        principalTable: "empresas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "eventos_chamado",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    chamado_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ocorrido_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    autor_id = table.Column<Guid>(type: "uuid", nullable: true),
                    tipo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    motivo = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    sequencia = table.Column<long>(type: "bigint", nullable: false),
                    antes = table.Column<string>(type: "jsonb", nullable: false),
                    depois = table.Column<string>(type: "jsonb", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_eventos_chamado", x => new { x.empresa_id, x.id });
                    table.ForeignKey(
                        name: "FK_eventos_chamado_chamados_empresa_id_chamado_id",
                        columns: x => new { x.empresa_id, x.chamado_id },
                        principalTable: "chamados",
                        principalColumns: new[] { "empresa_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_eventos_chamado_empresas_empresa_id",
                        column: x => x.empresa_id,
                        principalTable: "empresas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "mensagens",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    autor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    texto_original = table.Column<string>(type: "character varying(20000)", maxLength: 20000, nullable: false),
                    criada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    chamado_id = table.Column<Guid>(type: "uuid", nullable: true),
                    anexos = table.Column<string>(type: "jsonb", nullable: false),
                    correcoes = table.Column<string>(type: "jsonb", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mensagens", x => new { x.empresa_id, x.id });
                    table.ForeignKey(
                        name: "FK_mensagens_chamados_empresa_id_chamado_id",
                        columns: x => new { x.empresa_id, x.chamado_id },
                        principalTable: "chamados",
                        principalColumns: new[] { "empresa_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_mensagens_empresas_empresa_id",
                        column: x => x.empresa_id,
                        principalTable: "empresas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "categorias",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    pai_id = table.Column<Guid>(type: "uuid", nullable: true),
                    permite_chamados_com_subcategorias = table.Column<bool>(type: "boolean", nullable: false),
                    setor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ativa = table.Column<bool>(type: "boolean", nullable: false),
                    fila_destino_id = table.Column<Guid>(type: "uuid", nullable: true),
                    visibilidade_padrao = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    pre_qualificacoes = table.Column<string>(type: "jsonb", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_categorias", x => new { x.empresa_id, x.id });
                    table.ForeignKey(
                        name: "FK_categorias_categorias_empresa_id_pai_id",
                        columns: x => new { x.empresa_id, x.pai_id },
                        principalTable: "categorias",
                        principalColumns: new[] { "empresa_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_categorias_empresas_empresa_id",
                        column: x => x.empresa_id,
                        principalTable: "empresas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_categorias_filas_empresa_id_fila_destino_id",
                        columns: x => new { x.empresa_id, x.fila_destino_id },
                        principalTable: "filas",
                        principalColumns: new[] { "empresa_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_categorias_setores_empresa_id_setor_id",
                        columns: x => new { x.empresa_id, x.setor_id },
                        principalTable: "setores",
                        principalColumns: new[] { "empresa_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "membros_fila",
                columns: table => new
                {
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fila_id = table.Column<Guid>(type: "uuid", nullable: false),
                    vinculo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    autorizado_para_restritos = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_membros_fila", x => new { x.empresa_id, x.fila_id, x.usuario_id });
                    table.ForeignKey(
                        name: "FK_membros_fila_filas_empresa_id_fila_id",
                        columns: x => new { x.empresa_id, x.fila_id },
                        principalTable: "filas",
                        principalColumns: new[] { "empresa_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_membros_fila_vinculos_setor_empresa_id_vinculo_id",
                        columns: x => new { x.empresa_id, x.vinculo_id },
                        principalTable: "vinculos_setor",
                        principalColumns: new[] { "empresa_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ciclos_sla",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    setor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    iniciado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    finalizado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    motivo_finalizacao = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    meta_aplicada = table.Column<decimal>(type: "numeric", nullable: false),
                    versao_politica_aplicada = table.Column<long>(type: "bigint", nullable: false),
                    calendario_aplicado = table.Column<string>(type: "jsonb", nullable: false),
                    ciclo_atendimento_id = table.Column<Guid>(type: "uuid", nullable: true),
                    pausas = table.Column<string>(type: "jsonb", nullable: false),
                    revisoes_meta = table.Column<string>(type: "jsonb", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ciclos_sla", x => new { x.empresa_id, x.id });
                    table.ForeignKey(
                        name: "FK_ciclos_sla_ciclos_atendimento_empresa_id_ciclo_atendimento_~",
                        columns: x => new { x.empresa_id, x.ciclo_atendimento_id },
                        principalTable: "ciclos_atendimento",
                        principalColumns: new[] { "empresa_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ciclos_sla_empresas_empresa_id",
                        column: x => x.empresa_id,
                        principalTable: "empresas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "periodos_etapa",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    calendario_aplicado = table.Column<string>(type: "jsonb", nullable: false),
                    contexto = table.Column<string>(type: "jsonb", nullable: false),
                    entrada = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    saida = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ciclo_atendimento_id = table.Column<Guid>(type: "uuid", nullable: true),
                    fila_atual_id = table.Column<Guid>(type: "uuid", nullable: false, computedColumnSql: "(contexto -> 'Fila' ->> 'Id')::uuid", stored: true),
                    responsavel_atual_id = table.Column<Guid>(type: "uuid", nullable: true, computedColumnSql: "(contexto -> 'Responsavel' ->> 'Id')::uuid", stored: true),
                    setor_atual_id = table.Column<Guid>(type: "uuid", nullable: false, computedColumnSql: "(contexto -> 'Setor' ->> 'Id')::uuid", stored: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_periodos_etapa", x => new { x.empresa_id, x.id });
                    table.ForeignKey(
                        name: "FK_periodos_etapa_ciclos_atendimento_empresa_id_ciclo_atendime~",
                        columns: x => new { x.empresa_id, x.ciclo_atendimento_id },
                        principalTable: "ciclos_atendimento",
                        principalColumns: new[] { "empresa_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_periodos_etapa_empresas_empresa_id",
                        column: x => x.empresa_id,
                        principalTable: "empresas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_categorias_empresa_id_fila_destino_id",
                table: "categorias",
                columns: new[] { "empresa_id", "fila_destino_id" });

            migrationBuilder.CreateIndex(
                name: "IX_categorias_empresa_id_pai_id",
                table: "categorias",
                columns: new[] { "empresa_id", "pai_id" });

            migrationBuilder.CreateIndex(
                name: "IX_categorias_empresa_id_setor_id_pai_id_nome",
                table: "categorias",
                columns: new[] { "empresa_id", "setor_id", "pai_id", "nome" },
                unique: true,
                filter: "ativa = TRUE")
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_chamados_empresa_id_fila_atual_id_status_aberto_em_id",
                table: "chamados",
                columns: new[] { "empresa_id", "fila_atual_id", "status", "aberto_em", "id" },
                filter: "status NOT IN ('Encerrado', 'Cancelado', 'Resolvido')");

            migrationBuilder.CreateIndex(
                name: "IX_chamados_empresa_id_responsavel_atual_id_status_aberto_em_id",
                table: "chamados",
                columns: new[] { "empresa_id", "responsavel_atual_id", "status", "aberto_em", "id" });

            migrationBuilder.CreateIndex(
                name: "IX_chamados_empresa_id_solicitante_id",
                table: "chamados",
                columns: new[] { "empresa_id", "solicitante_id" });

            migrationBuilder.CreateIndex(
                name: "IX_chamados_empresa_id_status_limite_validacao",
                table: "chamados",
                columns: new[] { "empresa_id", "status", "limite_validacao" },
                filter: "status = 'Resolvido'");

            migrationBuilder.CreateIndex(
                name: "IX_ciclos_atendimento_empresa_id_chamado_id_numero",
                table: "ciclos_atendimento",
                columns: new[] { "empresa_id", "chamado_id", "numero" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ciclos_sla_empresa_id_ciclo_atendimento_id_tipo",
                table: "ciclos_sla",
                columns: new[] { "empresa_id", "ciclo_atendimento_id", "tipo" },
                filter: "finalizado_em IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ciclos_sla_empresa_id_setor_id_tipo_iniciado_em",
                table: "ciclos_sla",
                columns: new[] { "empresa_id", "setor_id", "tipo", "iniciado_em" });

            migrationBuilder.CreateIndex(
                name: "IX_eventos_chamado_empresa_id_chamado_id_sequencia",
                table: "eventos_chamado",
                columns: new[] { "empresa_id", "chamado_id", "sequencia" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_filas_empresa_id_setor_id_nome",
                table: "filas",
                columns: new[] { "empresa_id", "setor_id", "nome" },
                unique: true,
                filter: "ativa = TRUE");

            migrationBuilder.CreateIndex(
                name: "IX_membros_fila_empresa_id_vinculo_id",
                table: "membros_fila",
                columns: new[] { "empresa_id", "vinculo_id" });

            migrationBuilder.CreateIndex(
                name: "IX_mensagens_empresa_id_chamado_id_criada_em_id",
                table: "mensagens",
                columns: new[] { "empresa_id", "chamado_id", "criada_em", "id" });

            migrationBuilder.CreateIndex(
                name: "IX_outbox_empresa_id_criada_em_id",
                table: "outbox",
                columns: new[] { "empresa_id", "criada_em", "id" },
                filter: "processada_em IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_periodos_etapa_empresa_id_ciclo_atendimento_id",
                table: "periodos_etapa",
                columns: new[] { "empresa_id", "ciclo_atendimento_id" },
                filter: "saida IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_periodos_etapa_empresa_id_setor_atual_id_status_entrada",
                table: "periodos_etapa",
                columns: new[] { "empresa_id", "setor_atual_id", "status", "entrada" });

            migrationBuilder.CreateIndex(
                name: "IX_setores_empresa_id_politica_sla_id",
                table: "setores",
                columns: new[] { "empresa_id", "politica_sla_id" });

            migrationBuilder.CreateIndex(
                name: "IX_vinculos_setor_empresa_id_setor_id",
                table: "vinculos_setor",
                columns: new[] { "empresa_id", "setor_id" });

            migrationBuilder.CreateIndex(
                name: "IX_vinculos_setor_empresa_id_usuario_id_setor_id",
                table: "vinculos_setor",
                columns: new[] { "empresa_id", "usuario_id", "setor_id" },
                unique: true,
                filter: "ativo = TRUE");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "categorias");

            migrationBuilder.DropTable(
                name: "ciclos_sla");

            migrationBuilder.DropTable(
                name: "eventos_chamado");

            migrationBuilder.DropTable(
                name: "membros_fila");

            migrationBuilder.DropTable(
                name: "mensagens");

            migrationBuilder.DropTable(
                name: "outbox");

            migrationBuilder.DropTable(
                name: "periodos_etapa");

            migrationBuilder.DropTable(
                name: "filas");

            migrationBuilder.DropTable(
                name: "vinculos_setor");

            migrationBuilder.DropTable(
                name: "ciclos_atendimento");

            migrationBuilder.DropTable(
                name: "setores");

            migrationBuilder.DropTable(
                name: "chamados");

            migrationBuilder.DropTable(
                name: "politicas_sla");

            migrationBuilder.DropTable(
                name: "usuarios");

            migrationBuilder.DropTable(
                name: "empresas");
        }
    }
}
