using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace v8desk.infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class HierarquiaCategoriasConsulta : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_membros_fila_filas_empresa_id_fila_id",
                table: "membros_fila");

            migrationBuilder.DropForeignKey(
                name: "FK_membros_fila_vinculos_setor_empresa_id_vinculo_id",
                table: "membros_fila");

            migrationBuilder.DropIndex(
                name: "IX_vinculos_setor_empresa_id_setor_id",
                table: "vinculos_setor");

            migrationBuilder.DropIndex(
                name: "IX_membros_fila_empresa_id_vinculo_id",
                table: "membros_fila");

            migrationBuilder.AddColumn<Guid>(
                name: "setor_id",
                table: "membros_fila",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.Sql("UPDATE membros_fila AS m SET setor_id = f.setor_id FROM filas AS f WHERE f.empresa_id = m.empresa_id AND f.id = m.fila_id;");

            migrationBuilder.AddColumn<string>(
                name: "categoria_caminho_ids",
                table: "chamados",
                type: "jsonb",
                nullable: true,
                computedColumnSql: "jsonb_path_query_array(categoria_atual, '$[*].Id')",
                stored: true);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_vinculos_setor_empresa_id_setor_id_usuario_id_id",
                table: "vinculos_setor",
                columns: new[] { "empresa_id", "setor_id", "usuario_id", "id" });

            migrationBuilder.CreateIndex(
                name: "IX_membros_fila_empresa_id_setor_id_fila_id",
                table: "membros_fila",
                columns: new[] { "empresa_id", "setor_id", "fila_id" });

            migrationBuilder.CreateIndex(
                name: "IX_membros_fila_empresa_id_setor_id_usuario_id_vinculo_id",
                table: "membros_fila",
                columns: new[] { "empresa_id", "setor_id", "usuario_id", "vinculo_id" });

            migrationBuilder.CreateIndex(
                name: "IX_chamados_categoria_caminho_ids",
                table: "chamados",
                column: "categoria_caminho_ids")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.AddForeignKey(
                name: "FK_membros_fila_filas_empresa_id_setor_id_fila_id",
                table: "membros_fila",
                columns: new[] { "empresa_id", "setor_id", "fila_id" },
                principalTable: "filas",
                principalColumns: new[] { "empresa_id", "setor_id", "id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_membros_fila_vinculos_setor_empresa_id_setor_id_usuario_id_~",
                table: "membros_fila",
                columns: new[] { "empresa_id", "setor_id", "usuario_id", "vinculo_id" },
                principalTable: "vinculos_setor",
                principalColumns: new[] { "empresa_id", "setor_id", "usuario_id", "id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_membros_fila_filas_empresa_id_setor_id_fila_id",
                table: "membros_fila");

            migrationBuilder.DropForeignKey(
                name: "FK_membros_fila_vinculos_setor_empresa_id_setor_id_usuario_id_~",
                table: "membros_fila");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_vinculos_setor_empresa_id_setor_id_usuario_id_id",
                table: "vinculos_setor");

            migrationBuilder.DropIndex(
                name: "IX_membros_fila_empresa_id_setor_id_fila_id",
                table: "membros_fila");

            migrationBuilder.DropIndex(
                name: "IX_membros_fila_empresa_id_setor_id_usuario_id_vinculo_id",
                table: "membros_fila");

            migrationBuilder.DropIndex(
                name: "IX_chamados_categoria_caminho_ids",
                table: "chamados");

            migrationBuilder.DropColumn(
                name: "categoria_caminho_ids",
                table: "chamados");

            migrationBuilder.DropColumn(
                name: "setor_id",
                table: "membros_fila");

            migrationBuilder.CreateIndex(
                name: "IX_vinculos_setor_empresa_id_setor_id",
                table: "vinculos_setor",
                columns: new[] { "empresa_id", "setor_id" });

            migrationBuilder.CreateIndex(
                name: "IX_membros_fila_empresa_id_vinculo_id",
                table: "membros_fila",
                columns: new[] { "empresa_id", "vinculo_id" });

            migrationBuilder.AddForeignKey(
                name: "FK_membros_fila_filas_empresa_id_fila_id",
                table: "membros_fila",
                columns: new[] { "empresa_id", "fila_id" },
                principalTable: "filas",
                principalColumns: new[] { "empresa_id", "id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_membros_fila_vinculos_setor_empresa_id_vinculo_id",
                table: "membros_fila",
                columns: new[] { "empresa_id", "vinculo_id" },
                principalTable: "vinculos_setor",
                principalColumns: new[] { "empresa_id", "id" },
                onDelete: ReferentialAction.Restrict);
        }
    }
}
