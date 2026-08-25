using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Paghe.Migrazioni.Postgres.Migrazioni
{
    /// <inheritdoc />
    public partial class SaldiRolQuattordicesimaEProfiloContributivo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MesiAnzianitaRolRidotto",
                table: "RegoleCcnl",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MesiAnzianitaSenzaRol",
                table: "RegoleCcnl",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OreExFestivitaAnno",
                table: "RegoleCcnl",
                type: "numeric(8,4)",
                precision: 8,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OreRolAnno",
                table: "RegoleCcnl",
                type: "numeric(8,4)",
                precision: 8,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OreRolAnnoOltre15",
                table: "RegoleCcnl",
                type: "numeric(8,4)",
                precision: 8,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PercentualeRolRidotta",
                table: "RegoleCcnl",
                type: "numeric(6,4)",
                precision: 6,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "DataDecorrenzaDomicilioFiscale",
                table: "Rapporti",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ExFestivitaResidueIniziali",
                table: "Rapporti",
                type: "numeric(15,2)",
                precision: 15,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RiferimentoProgressiviFiscaliPregressi",
                table: "Rapporti",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RolResiduiIniziali",
                table: "Rapporti",
                type: "numeric(15,2)",
                precision: 15,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ExFestivitaGodute",
                table: "Cedolini",
                type: "numeric(15,2)",
                precision: 15,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ExFestivitaMaturate",
                table: "Cedolini",
                type: "numeric(15,2)",
                precision: 15,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ExFestivitaResidue",
                table: "Cedolini",
                type: "numeric(15,2)",
                precision: 15,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ExFestivitaSaldoPrecedente",
                table: "Cedolini",
                type: "numeric(15,2)",
                precision: 15,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PrestazioniRecuperabili",
                table: "Cedolini",
                type: "numeric(15,2)",
                precision: 15,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RetribuzioneFissa",
                table: "Cedolini",
                type: "numeric(15,2)",
                precision: 15,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RolGoduti",
                table: "Cedolini",
                type: "numeric(15,2)",
                precision: 15,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RolMaturati",
                table: "Cedolini",
                type: "numeric(15,2)",
                precision: 15,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RolResidui",
                table: "Cedolini",
                type: "numeric(15,2)",
                precision: 15,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RolSaldoPrecedente",
                table: "Cedolini",
                type: "numeric(15,2)",
                precision: 15,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TariffaGiornaliera",
                table: "Cedolini",
                type: "numeric(15,6)",
                precision: 15,
                scale: 6,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TariffaOraria",
                table: "Cedolini",
                type: "numeric(15,6)",
                precision: 15,
                scale: 6,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateOnly>(
                name: "DataPagamentoQuattordicesima",
                table: "Cartellini",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RiferimentoQuattordicesimaSeparata",
                table: "Cartellini",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AliquotaInpsDipendente",
                table: "Aziende",
                type: "numeric(8,6)",
                precision: 8,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CodiceAgevolazioneContributiva",
                table: "Aziende",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CodiceFondoIntegrazioneSalariale",
                table: "Aziende",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CodiciAutorizzazioneInps",
                table: "Aziende",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FonteProfiloContributivo",
                table: "Aziende",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RiferimentoTerzoElementoTerritoriale",
                table: "Aziende",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RiferimentoVerificaApplicabilitaCcnl",
                table: "Aziende",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RiferimentoVisuraCamerale",
                table: "Aziende",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StatoAlboArtigiani",
                table: "Aziende",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MesiAnzianitaRolRidotto",
                table: "RegoleCcnl");

            migrationBuilder.DropColumn(
                name: "MesiAnzianitaSenzaRol",
                table: "RegoleCcnl");

            migrationBuilder.DropColumn(
                name: "OreExFestivitaAnno",
                table: "RegoleCcnl");

            migrationBuilder.DropColumn(
                name: "OreRolAnno",
                table: "RegoleCcnl");

            migrationBuilder.DropColumn(
                name: "OreRolAnnoOltre15",
                table: "RegoleCcnl");

            migrationBuilder.DropColumn(
                name: "PercentualeRolRidotta",
                table: "RegoleCcnl");

            migrationBuilder.DropColumn(
                name: "DataDecorrenzaDomicilioFiscale",
                table: "Rapporti");

            migrationBuilder.DropColumn(
                name: "ExFestivitaResidueIniziali",
                table: "Rapporti");

            migrationBuilder.DropColumn(
                name: "RiferimentoProgressiviFiscaliPregressi",
                table: "Rapporti");

            migrationBuilder.DropColumn(
                name: "RolResiduiIniziali",
                table: "Rapporti");

            migrationBuilder.DropColumn(
                name: "ExFestivitaGodute",
                table: "Cedolini");

            migrationBuilder.DropColumn(
                name: "ExFestivitaMaturate",
                table: "Cedolini");

            migrationBuilder.DropColumn(
                name: "ExFestivitaResidue",
                table: "Cedolini");

            migrationBuilder.DropColumn(
                name: "ExFestivitaSaldoPrecedente",
                table: "Cedolini");

            migrationBuilder.DropColumn(
                name: "PrestazioniRecuperabili",
                table: "Cedolini");

            migrationBuilder.DropColumn(
                name: "RetribuzioneFissa",
                table: "Cedolini");

            migrationBuilder.DropColumn(
                name: "RolGoduti",
                table: "Cedolini");

            migrationBuilder.DropColumn(
                name: "RolMaturati",
                table: "Cedolini");

            migrationBuilder.DropColumn(
                name: "RolResidui",
                table: "Cedolini");

            migrationBuilder.DropColumn(
                name: "RolSaldoPrecedente",
                table: "Cedolini");

            migrationBuilder.DropColumn(
                name: "TariffaGiornaliera",
                table: "Cedolini");

            migrationBuilder.DropColumn(
                name: "TariffaOraria",
                table: "Cedolini");

            migrationBuilder.DropColumn(
                name: "DataPagamentoQuattordicesima",
                table: "Cartellini");

            migrationBuilder.DropColumn(
                name: "RiferimentoQuattordicesimaSeparata",
                table: "Cartellini");

            migrationBuilder.DropColumn(
                name: "AliquotaInpsDipendente",
                table: "Aziende");

            migrationBuilder.DropColumn(
                name: "CodiceAgevolazioneContributiva",
                table: "Aziende");

            migrationBuilder.DropColumn(
                name: "CodiceFondoIntegrazioneSalariale",
                table: "Aziende");

            migrationBuilder.DropColumn(
                name: "CodiciAutorizzazioneInps",
                table: "Aziende");

            migrationBuilder.DropColumn(
                name: "FonteProfiloContributivo",
                table: "Aziende");

            migrationBuilder.DropColumn(
                name: "RiferimentoTerzoElementoTerritoriale",
                table: "Aziende");

            migrationBuilder.DropColumn(
                name: "RiferimentoVerificaApplicabilitaCcnl",
                table: "Aziende");

            migrationBuilder.DropColumn(
                name: "RiferimentoVisuraCamerale",
                table: "Aziende");

            migrationBuilder.DropColumn(
                name: "StatoAlboArtigiani",
                table: "Aziende");
        }
    }
}
