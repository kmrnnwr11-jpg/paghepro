using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Paghe.Migrazioni.Postgres.Migrazioni
{
    /// <inheritdoc />
    public partial class CorrezioniRevisioneCommercio : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "OrePermessiMese",
                table: "RegoleCcnl",
                type: "numeric(8,4)",
                precision: 8,
                scale: 4,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(8,2)",
                oldPrecision: 8,
                oldScale: 2,
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MeseErogazioneQuattordicesima",
                table: "RegoleCcnl",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MeseErogazioneTredicesima",
                table: "RegoleCcnl",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OrePermessiMeseOltre15",
                table: "RegoleCcnl",
                type: "numeric(8,4)",
                precision: 8,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ScattiMassimo",
                table: "RegoleCcnl",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ScattiOgniMesi",
                table: "RegoleCcnl",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProvinciaLavoro",
                table: "Rapporti",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CuneoRedditoAzzeramento",
                table: "ParametriAnno",
                type: "numeric(15,2)",
                precision: 15,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CuneoRedditoFinoA",
                table: "ParametriAnno",
                type: "numeric(15,2)",
                precision: 15,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CuneoRedditoOltre",
                table: "ParametriAnno",
                type: "numeric(15,2)",
                precision: 15,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CuneoUlterioreDetrazioneAnnua",
                table: "ParametriAnno",
                type: "numeric(15,2)",
                precision: 15,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CostoMedioAnnualizzato",
                table: "Cedolini",
                type: "numeric(15,2)",
                precision: 15,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Contingenza",
                table: "CcnlLivelli",
                type: "numeric(15,2)",
                precision: 15,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AliquotaInpsDatore",
                table: "Aziende",
                type: "numeric(8,6)",
                precision: 8,
                scale: 6,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CcnlTerzoElemento",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CcnlCodice = table.Column<string>(type: "text", nullable: false),
                    Anno = table.Column<int>(type: "integer", nullable: false),
                    Provincia = table.Column<string>(type: "text", nullable: false),
                    Importo = table.Column<decimal>(type: "numeric(15,2)", precision: 15, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CcnlTerzoElemento", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CcnlTerzoElemento_CcnlCodice_Anno_Provincia",
                table: "CcnlTerzoElemento",
                columns: new[] { "CcnlCodice", "Anno", "Provincia" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CcnlTerzoElemento");

            migrationBuilder.DropColumn(
                name: "MeseErogazioneQuattordicesima",
                table: "RegoleCcnl");

            migrationBuilder.DropColumn(
                name: "MeseErogazioneTredicesima",
                table: "RegoleCcnl");

            migrationBuilder.DropColumn(
                name: "OrePermessiMeseOltre15",
                table: "RegoleCcnl");

            migrationBuilder.DropColumn(
                name: "ScattiMassimo",
                table: "RegoleCcnl");

            migrationBuilder.DropColumn(
                name: "ScattiOgniMesi",
                table: "RegoleCcnl");

            migrationBuilder.DropColumn(
                name: "ProvinciaLavoro",
                table: "Rapporti");

            migrationBuilder.DropColumn(
                name: "CuneoRedditoAzzeramento",
                table: "ParametriAnno");

            migrationBuilder.DropColumn(
                name: "CuneoRedditoFinoA",
                table: "ParametriAnno");

            migrationBuilder.DropColumn(
                name: "CuneoRedditoOltre",
                table: "ParametriAnno");

            migrationBuilder.DropColumn(
                name: "CuneoUlterioreDetrazioneAnnua",
                table: "ParametriAnno");

            migrationBuilder.DropColumn(
                name: "CostoMedioAnnualizzato",
                table: "Cedolini");

            migrationBuilder.DropColumn(
                name: "Contingenza",
                table: "CcnlLivelli");

            migrationBuilder.DropColumn(
                name: "AliquotaInpsDatore",
                table: "Aziende");

            migrationBuilder.AlterColumn<decimal>(
                name: "OrePermessiMese",
                table: "RegoleCcnl",
                type: "numeric(8,2)",
                precision: 8,
                scale: 2,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(8,4)",
                oldPrecision: 8,
                oldScale: 4,
                oldNullable: true);
        }
    }
}
