using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace v8desk.infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PaginacaoChamados : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "numero",
                table: "chamados",
                type: "bigint",
                nullable: false,
                defaultValue: 0L)
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "proximo_vencimento_em",
                table: "chamados",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_chamados_empresa_id_fila_atual_id_atualizado_em_id",
                table: "chamados",
                columns: new[] { "empresa_id", "fila_atual_id", "atualizado_em", "id" });

            migrationBuilder.CreateIndex(
                name: "IX_chamados_empresa_id_fila_atual_id_proximo_vencimento_em_id",
                table: "chamados",
                columns: new[] { "empresa_id", "fila_atual_id", "proximo_vencimento_em", "id" },
                filter: "status IN ('AguardandoTriagem', 'Aceito', 'EmAtendimento', 'AguardandoInformacao')");

            migrationBuilder.CreateIndex(
                name: "IX_chamados_empresa_id_numero",
                table: "chamados",
                columns: new[] { "empresa_id", "numero" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_chamados_empresa_id_responsavel_atual_id_status_proximo_ven~",
                table: "chamados",
                columns: new[] { "empresa_id", "responsavel_atual_id", "status", "proximo_vencimento_em", "id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_chamados_empresa_id_fila_atual_id_atualizado_em_id",
                table: "chamados");

            migrationBuilder.DropIndex(
                name: "IX_chamados_empresa_id_fila_atual_id_proximo_vencimento_em_id",
                table: "chamados");

            migrationBuilder.DropIndex(
                name: "IX_chamados_empresa_id_numero",
                table: "chamados");

            migrationBuilder.DropIndex(
                name: "IX_chamados_empresa_id_responsavel_atual_id_status_proximo_ven~",
                table: "chamados");

            migrationBuilder.DropColumn(
                name: "numero",
                table: "chamados");

            migrationBuilder.DropColumn(
                name: "proximo_vencimento_em",
                table: "chamados");
        }
    }
}
