using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Paghe.Migrazioni.Sqlite.Migrazioni
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
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MesiAnzianitaSenzaRol",
                table: "RegoleCcnl",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OreExFestivitaAnno",
                table: "RegoleCcnl",
                type: "TEXT",
                precision: 8,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OreRolAnno",
                table: "RegoleCcnl",
                type: "TEXT",
                precision: 8,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OreRolAnnoOltre15",
                table: "RegoleCcnl",
                type: "TEXT",
                precision: 8,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PercentualeRolRidotta",
                table: "RegoleCcnl",
                type: "TEXT",
                precision: 6,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "DataDecorrenzaDomicilioFiscale",
                table: "Rapporti",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ExFestivitaResidueIniziali",
                table: "Rapporti",
                type: "TEXT",
                precision: 15,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RiferimentoProgressiviFiscaliPregressi",
                table: "Rapporti",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RolResiduiIniziali",
                table: "Rapporti",
                type: "TEXT",
                precision: 15,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ExFestivitaGodute",
                table: "Cedolini",
                type: "TEXT",
                precision: 15,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ExFestivitaMaturate",
                table: "Cedolini",
                type: "TEXT",
                precision: 15,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ExFestivitaResidue",
                table: "Cedolini",
                type: "TEXT",
                precision: 15,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ExFestivitaSaldoPrecedente",
                table: "Cedolini",
                type: "TEXT",
                precision: 15,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PrestazioniRecuperabili",
                table: "Cedolini",
                type: "TEXT",
                precision: 15,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RetribuzioneFissa",
                table: "Cedolini",
                type: "TEXT",
                precision: 15,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RolGoduti",
                table: "Cedolini",
                type: "TEXT",
                precision: 15,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RolMaturati",
                table: "Cedolini",
                type: "TEXT",
                precision: 15,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RolResidui",
                table: "Cedolini",
                type: "TEXT",
                precision: 15,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RolSaldoPrecedente",
                table: "Cedolini",
                type: "TEXT",
                precision: 15,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TariffaGiornaliera",
                table: "Cedolini",
                type: "TEXT",
                precision: 15,
                scale: 6,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TariffaOraria",
                table: "Cedolini",
                type: "TEXT",
                precision: 15,
                scale: 6,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateOnly>(
                name: "DataPagamentoQuattordicesima",
                table: "Cartellini",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RiferimentoQuattordicesimaSeparata",
                table: "Cartellini",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AliquotaInpsDipendente",
                table: "Aziende",
                type: "TEXT",
                precision: 8,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CodiceAgevolazioneContributiva",
                table: "Aziende",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CodiceFondoIntegrazioneSalariale",
                table: "Aziende",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CodiciAutorizzazioneInps",
                table: "Aziende",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FonteProfiloContributivo",
                table: "Aziende",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RiferimentoTerzoElementoTerritoriale",
                table: "Aziende",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RiferimentoVerificaApplicabilitaCcnl",
                table: "Aziende",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RiferimentoVisuraCamerale",
                table: "Aziende",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StatoAlboArtigiani",
                table: "Aziende",
                type: "TEXT",
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
