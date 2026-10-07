using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace v8desk.infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class IntegracoesEmailMicrosoft : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "auditoria_integracoes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    acao = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ocorrida_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_auditoria_integracoes", x => new { x.empresa_id, x.id });
                    table.ForeignKey(
                        name: "FK_auditoria_integracoes_empresas_empresa_id",
                        column: x => x.empresa_id,
                        principalTable: "empresas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_auditoria_integracoes_usuarios_empresa_id_usuario_id",
                        columns: x => new { x.empresa_id, x.usuario_id },
                        principalTable: "usuarios",
                        principalColumns: new[] { "empresa_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "contatos_notificacao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_contatos_notificacao", x => new { x.empresa_id, x.id });
                    table.ForeignKey(
                        name: "FK_contatos_notificacao_empresas_empresa_id",
                        column: x => x.empresa_id,
                        principalTable: "empresas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_contatos_notificacao_usuarios_empresa_id_usuario_id",
                        columns: x => new { x.empresa_id, x.usuario_id },
                        principalTable: "usuarios",
                        principalColumns: new[] { "empresa_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "entregas_email",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    evento_id = table.Column<Guid>(type: "uuid", nullable: true),
                    chamado_id = table.Column<Guid>(type: "uuid", nullable: true),
                    teste = table.Column<bool>(type: "boolean", nullable: false),
                    revisao_configuracao = table.Column<long>(type: "bigint", nullable: false),
                    situacao = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    tentativas = table.Column<int>(type: "integer", nullable: false),
                    criada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    proxima_tentativa_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    codigo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_entregas_email", x => new { x.empresa_id, x.id });
                    table.ForeignKey(
                        name: "FK_entregas_email_chamados_empresa_id_chamado_id",
                        columns: x => new { x.empresa_id, x.chamado_id },
                        principalTable: "chamados",
                        principalColumns: new[] { "empresa_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_entregas_email_empresas_empresa_id",
                        column: x => x.empresa_id,
                        principalTable: "empresas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_entregas_email_usuarios_empresa_id_usuario_id",
                        columns: x => new { x.empresa_id, x.usuario_id },
                        principalTable: "usuarios",
                        principalColumns: new[] { "empresa_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "integracoes_empresa",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "jsonb", nullable: true),
                    microsoft = table.Column<string>(type: "jsonb", nullable: true),
                    tenant_microsoft_id = table.Column<Guid>(type: "uuid", nullable: true),
                    api_microsoft_id = table.Column<Guid>(type: "uuid", nullable: true),
                    email_ativo = table.Column<bool>(type: "boolean", nullable: false),
                    microsoft_ativo = table.Column<bool>(type: "boolean", nullable: false),
                    revisao_email = table.Column<long>(type: "bigint", nullable: false),
                    revisao_email_validada = table.Column<long>(type: "bigint", nullable: true),
                    ultimo_teste_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ultimo_diagnostico = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_integracoes_empresa", x => new { x.empresa_id, x.id });
                    table.ForeignKey(
                        name: "FK_integracoes_empresa_empresas_empresa_id",
                        column: x => x.empresa_id,
                        principalTable: "empresas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "vinculos_microsoft",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    objeto_id = table.Column<Guid>(type: "uuid", nullable: false),
                    administrador = table.Column<bool>(type: "boolean", nullable: false),
                    ativo = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vinculos_microsoft", x => new { x.empresa_id, x.id });
                    table.ForeignKey(
                        name: "FK_vinculos_microsoft_empresas_empresa_id",
                        column: x => x.empresa_id,
                        principalTable: "empresas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_vinculos_microsoft_usuarios_empresa_id_usuario_id",
                        columns: x => new { x.empresa_id, x.usuario_id },
                        principalTable: "usuarios",
                        principalColumns: new[] { "empresa_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_auditoria_integracoes_empresa_id_ocorrida_em",
                table: "auditoria_integracoes",
                columns: new[] { "empresa_id", "ocorrida_em" });

            migrationBuilder.CreateIndex(
                name: "IX_auditoria_integracoes_empresa_id_usuario_id",
                table: "auditoria_integracoes",
                columns: new[] { "empresa_id", "usuario_id" });

            migrationBuilder.CreateIndex(
                name: "IX_contatos_notificacao_empresa_id_usuario_id",
                table: "contatos_notificacao",
                columns: new[] { "empresa_id", "usuario_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_entregas_email_empresa_id_chamado_id",
                table: "entregas_email",
                columns: new[] { "empresa_id", "chamado_id" });

            migrationBuilder.CreateIndex(
                name: "IX_entregas_email_empresa_id_criada_em_id",
                table: "entregas_email",
                columns: new[] { "empresa_id", "criada_em", "id" });

            migrationBuilder.CreateIndex(
                name: "IX_entregas_email_empresa_id_evento_id_usuario_id",
                table: "entregas_email",
                columns: new[] { "empresa_id", "evento_id", "usuario_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_entregas_email_empresa_id_proxima_tentativa_em",
                table: "entregas_email",
                columns: new[] { "empresa_id", "proxima_tentativa_em" },
                filter: "situacao = 'Pendente'");

            migrationBuilder.CreateIndex(
                name: "IX_entregas_email_empresa_id_usuario_id",
                table: "entregas_email",
                columns: new[] { "empresa_id", "usuario_id" });

            migrationBuilder.CreateIndex(
                name: "IX_integracoes_empresa_empresa_id",
                table: "integracoes_empresa",
                column: "empresa_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_integracoes_empresa_tenant_microsoft_id_api_microsoft_id",
                table: "integracoes_empresa",
                columns: new[] { "tenant_microsoft_id", "api_microsoft_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_vinculos_microsoft_empresa_id_tenant_id_objeto_id",
                table: "vinculos_microsoft",
                columns: new[] { "empresa_id", "tenant_id", "objeto_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_vinculos_microsoft_empresa_id_usuario_id_tenant_id",
                table: "vinculos_microsoft",
                columns: new[] { "empresa_id", "usuario_id", "tenant_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "auditoria_integracoes");

            migrationBuilder.DropTable(
                name: "contatos_notificacao");

            migrationBuilder.DropTable(
                name: "entregas_email");

            migrationBuilder.DropTable(
                name: "integracoes_empresa");

            migrationBuilder.DropTable(
                name: "vinculos_microsoft");
        }
    }
}
