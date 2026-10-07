using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace v8desk.infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FortalecerInfraestrutura : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_categorias_categorias_empresa_id_pai_id",
                table: "categorias");

            migrationBuilder.DropForeignKey(
                name: "FK_categorias_filas_empresa_id_fila_destino_id",
                table: "categorias");

            migrationBuilder.DropIndex(
                name: "IX_outbox_empresa_id_criada_em_id",
                table: "outbox");

            migrationBuilder.DropIndex(
                name: "IX_filas_empresa_id_setor_id_nome",
                table: "filas");

            migrationBuilder.DropIndex(
                name: "IX_categorias_empresa_id_fila_destino_id",
                table: "categorias");

            migrationBuilder.DropIndex(
                name: "IX_categorias_empresa_id_pai_id",
                table: "categorias");

            migrationBuilder.DropIndex(
                name: "IX_categorias_empresa_id_setor_id_pai_id_nome",
                table: "categorias");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "disponivel_em",
                table: "outbox",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<int>(
                name: "tentativas",
                table: "outbox",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ultimo_erro",
                table: "outbox",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "nome_comparacao",
                table: "filas",
                type: "character varying(400)",
                maxLength: 400,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "nome_comparacao",
                table: "categorias",
                type: "character varying(400)",
                maxLength: 400,
                nullable: false,
                defaultValue: "");

            // Preenche registros existentes antes de recriar índices únicos.
            migrationBuilder.Sql("UPDATE filas SET nome_comparacao = upper(nome); UPDATE categorias SET nome_comparacao = upper(nome); UPDATE outbox SET disponivel_em = criada_em;");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_filas_empresa_id_setor_id_id",
                table: "filas",
                columns: new[] { "empresa_id", "setor_id", "id" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_categorias_empresa_id_setor_id_id",
                table: "categorias",
                columns: new[] { "empresa_id", "setor_id", "id" });

            migrationBuilder.CreateTable(
                name: "comandos_executados",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    chave = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    resultado = table.Column<string>(type: "text", nullable: false),
                    criado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_comandos_executados", x => new { x.empresa_id, x.id });
                    table.ForeignKey(
                        name: "FK_comandos_executados_empresas_empresa_id",
                        column: x => x.empresa_id,
                        principalTable: "empresas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_comandos_executados_usuarios_empresa_id_usuario_id",
                        columns: x => new { x.empresa_id, x.usuario_id },
                        principalTable: "usuarios",
                        principalColumns: new[] { "empresa_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_periodo_datas",
                table: "periodos_etapa",
                sql: "saida IS NULL OR saida >= entrada");

            migrationBuilder.CreateIndex(
                name: "IX_outbox_empresa_id_disponivel_em_criada_em_id",
                table: "outbox",
                columns: new[] { "empresa_id", "disponivel_em", "criada_em", "id" },
                filter: "processada_em IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_filas_empresa_id_setor_id_nome_comparacao",
                table: "filas",
                columns: new[] { "empresa_id", "setor_id", "nome_comparacao" },
                unique: true,
                filter: "ativa = TRUE");

            migrationBuilder.AddCheckConstraint(
                name: "ck_evento_sequencia",
                table: "eventos_chamado",
                sql: "sequencia > 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_sla_datas",
                table: "ciclos_sla",
                sql: "finalizado_em IS NULL OR finalizado_em >= iniciado_em");

            migrationBuilder.AddCheckConstraint(
                name: "ck_sla_meta",
                table: "ciclos_sla",
                sql: "meta_aplicada > 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_ciclo_numero",
                table: "ciclos_atendimento",
                sql: "numero > 0");

            migrationBuilder.CreateIndex(
                name: "IX_chamados_empresa_id_fila_atual_id_aberto_em_id",
                table: "chamados",
                columns: new[] { "empresa_id", "fila_atual_id", "aberto_em", "id" });

            migrationBuilder.CreateIndex(
                name: "IX_categorias_empresa_id_setor_id_fila_destino_id",
                table: "categorias",
                columns: new[] { "empresa_id", "setor_id", "fila_destino_id" });

            migrationBuilder.CreateIndex(
                name: "IX_categorias_empresa_id_setor_id_pai_id_nome_comparacao",
                table: "categorias",
                columns: new[] { "empresa_id", "setor_id", "pai_id", "nome_comparacao" },
                unique: true,
                filter: "ativa = TRUE")
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_comandos_executados_empresa_id_usuario_id_chave",
                table: "comandos_executados",
                columns: new[] { "empresa_id", "usuario_id", "chave" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_categorias_categorias_empresa_id_setor_id_pai_id",
                table: "categorias",
                columns: new[] { "empresa_id", "setor_id", "pai_id" },
                principalTable: "categorias",
                principalColumns: new[] { "empresa_id", "setor_id", "id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_categorias_filas_empresa_id_setor_id_fila_destino_id",
                table: "categorias",
                columns: new[] { "empresa_id", "setor_id", "fila_destino_id" },
                principalTable: "filas",
                principalColumns: new[] { "empresa_id", "setor_id", "id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_categorias_categorias_empresa_id_setor_id_pai_id",
                table: "categorias");

            migrationBuilder.DropForeignKey(
                name: "FK_categorias_filas_empresa_id_setor_id_fila_destino_id",
                table: "categorias");

            migrationBuilder.DropTable(
                name: "comandos_executados");

            migrationBuilder.DropCheckConstraint(
                name: "ck_periodo_datas",
                table: "periodos_etapa");

            migrationBuilder.DropIndex(
                name: "IX_outbox_empresa_id_disponivel_em_criada_em_id",
                table: "outbox");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_filas_empresa_id_setor_id_id",
                table: "filas");

            migrationBuilder.DropIndex(
                name: "IX_filas_empresa_id_setor_id_nome_comparacao",
                table: "filas");

            migrationBuilder.DropCheckConstraint(
                name: "ck_evento_sequencia",
                table: "eventos_chamado");

            migrationBuilder.DropCheckConstraint(
                name: "ck_sla_datas",
                table: "ciclos_sla");

            migrationBuilder.DropCheckConstraint(
                name: "ck_sla_meta",
                table: "ciclos_sla");

            migrationBuilder.DropCheckConstraint(
                name: "ck_ciclo_numero",
                table: "ciclos_atendimento");

            migrationBuilder.DropIndex(
                name: "IX_chamados_empresa_id_fila_atual_id_aberto_em_id",
                table: "chamados");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_categorias_empresa_id_setor_id_id",
                table: "categorias");

            migrationBuilder.DropIndex(
                name: "IX_categorias_empresa_id_setor_id_fila_destino_id",
                table: "categorias");

            migrationBuilder.DropIndex(
                name: "IX_categorias_empresa_id_setor_id_pai_id_nome_comparacao",
                table: "categorias");

            migrationBuilder.DropColumn(
                name: "disponivel_em",
                table: "outbox");

            migrationBuilder.DropColumn(
                name: "tentativas",
                table: "outbox");

            migrationBuilder.DropColumn(
                name: "ultimo_erro",
                table: "outbox");

            migrationBuilder.DropColumn(
                name: "nome_comparacao",
                table: "filas");

            migrationBuilder.DropColumn(
                name: "nome_comparacao",
                table: "categorias");

            migrationBuilder.CreateIndex(
                name: "IX_outbox_empresa_id_criada_em_id",
                table: "outbox",
                columns: new[] { "empresa_id", "criada_em", "id" },
                filter: "processada_em IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_filas_empresa_id_setor_id_nome",
                table: "filas",
                columns: new[] { "empresa_id", "setor_id", "nome" },
                unique: true,
                filter: "ativa = TRUE");

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

            migrationBuilder.AddForeignKey(
                name: "FK_categorias_categorias_empresa_id_pai_id",
                table: "categorias",
                columns: new[] { "empresa_id", "pai_id" },
                principalTable: "categorias",
                principalColumns: new[] { "empresa_id", "id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_categorias_filas_empresa_id_fila_destino_id",
                table: "categorias",
                columns: new[] { "empresa_id", "fila_destino_id" },
                principalTable: "filas",
                principalColumns: new[] { "empresa_id", "id" },
                onDelete: ReferentialAction.Restrict);
        }
    }
}
