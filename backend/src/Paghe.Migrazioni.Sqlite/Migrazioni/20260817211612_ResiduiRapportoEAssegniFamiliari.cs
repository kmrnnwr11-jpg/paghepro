using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Paghe.Migrazioni.Sqlite.Migrazioni
{
    /// <inheritdoc />
    public partial class ResiduiRapportoEAssegniFamiliari : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "FerieResidueIniziali",
                table: "Rapporti",
                type: "TEXT",
                precision: 15,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PermessiResiduiIniziali",
                table: "Rapporti",
                type: "TEXT",
                precision: 15,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ImportoAssegniFamiliari",
                table: "Cartellini",
                type: "TEXT",
                precision: 15,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "StatoAssegniFamiliari",
                table: "Cartellini",
                type: "TEXT",
                nullable: false,
                defaultValue: "DaVerificare");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FerieResidueIniziali",
                table: "Rapporti");

            migrationBuilder.DropColumn(
                name: "PermessiResiduiIniziali",
                table: "Rapporti");

            migrationBuilder.DropColumn(
                name: "ImportoAssegniFamiliari",
                table: "Cartellini");

            migrationBuilder.DropColumn(
                name: "StatoAssegniFamiliari",
                table: "Cartellini");
        }
    }
}
