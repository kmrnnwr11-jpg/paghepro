using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Paghe.Migrazioni.Sqlite.Migrazioni
{
    /// <inheritdoc />
    public partial class LimitiElementiAgevolati : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "BuonoPastoCartaceoEsenteGiorno",
                table: "ParametriAnno",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "BuonoPastoElettronicoEsenteGiorno",
                table: "ParametriAnno",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "FringeBenefitSogliaAnnua",
                table: "ParametriAnno",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "FringeBenefitSogliaAnnuaConFigli",
                table: "ParametriAnno",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PremioRisultatoAliquota",
                table: "ParametriAnno",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PremioRisultatoLimiteAnnuo",
                table: "ParametriAnno",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PremioRisultatoRedditoMassimo",
                table: "ParametriAnno",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TrasfertaEsteroEsenteGiorno",
                table: "ParametriAnno",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TrasfertaItaliaEsenteGiorno",
                table: "ParametriAnno",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BuonoPastoCartaceoEsenteGiorno",
                table: "ParametriAnno");

            migrationBuilder.DropColumn(
                name: "BuonoPastoElettronicoEsenteGiorno",
                table: "ParametriAnno");

            migrationBuilder.DropColumn(
                name: "FringeBenefitSogliaAnnua",
                table: "ParametriAnno");

            migrationBuilder.DropColumn(
                name: "FringeBenefitSogliaAnnuaConFigli",
                table: "ParametriAnno");

            migrationBuilder.DropColumn(
                name: "PremioRisultatoAliquota",
                table: "ParametriAnno");

            migrationBuilder.DropColumn(
                name: "PremioRisultatoLimiteAnnuo",
                table: "ParametriAnno");

            migrationBuilder.DropColumn(
                name: "PremioRisultatoRedditoMassimo",
                table: "ParametriAnno");

            migrationBuilder.DropColumn(
                name: "TrasfertaEsteroEsenteGiorno",
                table: "ParametriAnno");

            migrationBuilder.DropColumn(
                name: "TrasfertaItaliaEsenteGiorno",
                table: "ParametriAnno");
        }
    }
}
