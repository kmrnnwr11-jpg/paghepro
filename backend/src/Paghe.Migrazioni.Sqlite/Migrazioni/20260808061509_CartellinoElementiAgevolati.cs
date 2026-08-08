using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Paghe.Migrazioni.Sqlite.Migrazioni
{
    /// <inheritdoc />
    public partial class CartellinoElementiAgevolati : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "BuonoPastoElettronico",
                table: "Cartellini",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "FigliACarico",
                table: "Cartellini",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "FringeBenefit",
                table: "Cartellini",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "FringeBenefitGiaErogatoAnno",
                table: "Cartellini",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "GiorniBuonoPasto",
                table: "Cartellini",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "GiorniTrasfertaEstero",
                table: "Cartellini",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "GiorniTrasfertaItalia",
                table: "Cartellini",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "IndennitaTrasfertaGiornaliera",
                table: "Cartellini",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PremioRisultato",
                table: "Cartellini",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RedditoAnnoPrecedente",
                table: "Cartellini",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RimborsiSpeseDocumentate",
                table: "Cartellini",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "RimborsoTrasferta",
                table: "Cartellini",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "ValoreBuonoPasto",
                table: "Cartellini",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BuonoPastoElettronico",
                table: "Cartellini");

            migrationBuilder.DropColumn(
                name: "FigliACarico",
                table: "Cartellini");

            migrationBuilder.DropColumn(
                name: "FringeBenefit",
                table: "Cartellini");

            migrationBuilder.DropColumn(
                name: "FringeBenefitGiaErogatoAnno",
                table: "Cartellini");

            migrationBuilder.DropColumn(
                name: "GiorniBuonoPasto",
                table: "Cartellini");

            migrationBuilder.DropColumn(
                name: "GiorniTrasfertaEstero",
                table: "Cartellini");

            migrationBuilder.DropColumn(
                name: "GiorniTrasfertaItalia",
                table: "Cartellini");

            migrationBuilder.DropColumn(
                name: "IndennitaTrasfertaGiornaliera",
                table: "Cartellini");

            migrationBuilder.DropColumn(
                name: "PremioRisultato",
                table: "Cartellini");

            migrationBuilder.DropColumn(
                name: "RedditoAnnoPrecedente",
                table: "Cartellini");

            migrationBuilder.DropColumn(
                name: "RimborsiSpeseDocumentate",
                table: "Cartellini");

            migrationBuilder.DropColumn(
                name: "RimborsoTrasferta",
                table: "Cartellini");

            migrationBuilder.DropColumn(
                name: "ValoreBuonoPasto",
                table: "Cartellini");
        }
    }
}
