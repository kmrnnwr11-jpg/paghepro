using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Paghe.Migrazioni.Postgres.Migrazioni
{
    /// <inheritdoc />
    public partial class AnagraficaContributivaUniEmens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CittadinanzaInps",
                table: "Rapporti",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "QualificaProfessionaleIstat",
                table: "Rapporti",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TipoContribuzioneInps",
                table: "Rapporti",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TipoPartTimeInps",
                table: "Rapporti",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CittadinanzaInps",
                table: "Rapporti");

            migrationBuilder.DropColumn(
                name: "QualificaProfessionaleIstat",
                table: "Rapporti");

            migrationBuilder.DropColumn(
                name: "TipoContribuzioneInps",
                table: "Rapporti");

            migrationBuilder.DropColumn(
                name: "TipoPartTimeInps",
                table: "Rapporti");
        }
    }
}
