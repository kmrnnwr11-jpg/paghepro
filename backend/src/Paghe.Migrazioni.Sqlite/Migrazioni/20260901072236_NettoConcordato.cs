using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Paghe.Migrazioni.Sqlite.Migrazioni
{
    /// <inheritdoc />
    public partial class NettoConcordato : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "NettoConcordatoDecorrenza",
                table: "Rapporti",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "NettoConcordatoMensile",
                table: "Rapporti",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NettoConcordatoDecorrenza",
                table: "Rapporti");

            migrationBuilder.DropColumn(
                name: "NettoConcordatoMensile",
                table: "Rapporti");
        }
    }
}
