using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Paghe.Migrazioni.Sqlite.Migrazioni
{
    /// <inheritdoc />
    public partial class CorrezioniRevisioneCommercio : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MeseErogazioneQuattordicesima",
                table: "RegoleCcnl",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MeseErogazioneTredicesima",
                table: "RegoleCcnl",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OrePermessiMeseOltre15",
                table: "RegoleCcnl",
                type: "TEXT",
                precision: 8,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ScattiMassimo",
                table: "RegoleCcnl",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ScattiOgniMesi",
                table: "RegoleCcnl",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProvinciaLavoro",
                table: "Rapporti",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CuneoRedditoAzzeramento",
                table: "ParametriAnno",
                type: "TEXT",
                precision: 15,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CuneoRedditoFinoA",
                table: "ParametriAnno",
                type: "TEXT",
                precision: 15,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CuneoRedditoOltre",
                table: "ParametriAnno",
                type: "TEXT",
                precision: 15,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CuneoUlterioreDetrazioneAnnua",
                table: "ParametriAnno",
                type: "TEXT",
                precision: 15,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CostoMedioAnnualizzato",
                table: "Cedolini",
                type: "TEXT",
                precision: 15,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Contingenza",
                table: "CcnlLivelli",
                type: "TEXT",
                precision: 15,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AliquotaInpsDatore",
                table: "Aziende",
                type: "TEXT",
                precision: 8,
                scale: 6,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CcnlTerzoElemento",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CcnlCodice = table.Column<string>(type: "TEXT", nullable: false),
                    Anno = table.Column<int>(type: "INTEGER", nullable: false),
                    Provincia = table.Column<string>(type: "TEXT", nullable: false),
                    Importo = table.Column<decimal>(type: "TEXT", precision: 15, scale: 2, nullable: false)
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
        }
    }
}
