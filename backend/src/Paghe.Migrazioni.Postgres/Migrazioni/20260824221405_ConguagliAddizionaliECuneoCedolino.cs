using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Paghe.Migrazioni.Postgres.Migrazioni
{
    /// <inheritdoc />
    public partial class ConguagliAddizionaliECuneoCedolino : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "AssistenzaSanitariaDatoreMese",
                table: "RegoleCcnl",
                type: "numeric(8,2)",
                precision: 8,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AssistenzaSanitariaLavoratoreMese",
                table: "RegoleCcnl",
                type: "numeric(8,2)",
                precision: 8,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "EnteBilateraleAliquotaDatore",
                table: "RegoleCcnl",
                type: "numeric(8,5)",
                precision: 8,
                scale: 5,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "EnteBilateraleAliquotaLavoratore",
                table: "RegoleCcnl",
                type: "numeric(8,5)",
                precision: 8,
                scale: 5,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AddizionaleComunaleAccontoAnnuo",
                table: "Rapporti",
                type: "numeric(15,2)",
                precision: 15,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AddizionaleComunaleSaldoAnnuo",
                table: "Rapporti",
                type: "numeric(15,2)",
                precision: 15,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AddizionaleRegionaleConguaglioAnnuo",
                table: "Rapporti",
                type: "numeric(15,2)",
                precision: 15,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "UlterioreDetrazioneCuneo",
                table: "Cedolini",
                type: "numeric(15,2)",
                precision: 15,
                scale: 2,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AssistenzaSanitariaDatoreMese",
                table: "RegoleCcnl");

            migrationBuilder.DropColumn(
                name: "AssistenzaSanitariaLavoratoreMese",
                table: "RegoleCcnl");

            migrationBuilder.DropColumn(
                name: "EnteBilateraleAliquotaDatore",
                table: "RegoleCcnl");

            migrationBuilder.DropColumn(
                name: "EnteBilateraleAliquotaLavoratore",
                table: "RegoleCcnl");

            migrationBuilder.DropColumn(
                name: "AddizionaleComunaleAccontoAnnuo",
                table: "Rapporti");

            migrationBuilder.DropColumn(
                name: "AddizionaleComunaleSaldoAnnuo",
                table: "Rapporti");

            migrationBuilder.DropColumn(
                name: "AddizionaleRegionaleConguaglioAnnuo",
                table: "Rapporti");

            migrationBuilder.DropColumn(
                name: "UlterioreDetrazioneCuneo",
                table: "Cedolini");
        }
    }
}
