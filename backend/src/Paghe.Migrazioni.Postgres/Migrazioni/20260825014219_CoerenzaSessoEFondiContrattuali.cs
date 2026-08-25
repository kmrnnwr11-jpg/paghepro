using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Paghe.Migrazioni.Postgres.Migrazioni
{
    /// <inheritdoc />
    public partial class CoerenzaSessoEFondiContrattuali : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "SolidarietaFondiAliquota",
                table: "ParametriAnno",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Sesso",
                table: "Dipendenti",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "AderisceFondiContrattuali",
                table: "Aziende",
                type: "boolean",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SolidarietaFondiAliquota",
                table: "ParametriAnno");

            migrationBuilder.DropColumn(
                name: "Sesso",
                table: "Dipendenti");

            migrationBuilder.DropColumn(
                name: "AderisceFondiContrattuali",
                table: "Aziende");
        }
    }
}
