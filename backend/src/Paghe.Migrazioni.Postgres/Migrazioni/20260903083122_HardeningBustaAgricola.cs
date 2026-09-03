using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Paghe.Migrazioni.Postgres.Migrazioni
{
    /// <inheritdoc />
    public partial class HardeningBustaAgricola : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "RedditoPresuntoAnnuo",
                table: "Rapporti",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "DeducibileIrpef",
                table: "ContributiAgricoli",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "ArrotondamentoDelMese",
                table: "CedoliniAgricoli",
                type: "numeric(15,2)",
                precision: 15,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "OrigineRedditoPresunto",
                table: "CedoliniAgricoli",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SegnalazioniJson",
                table: "CedoliniAgricoli",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RedditoPresuntoAgricoloAnnuo",
                table: "Aziende",
                type: "numeric",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ElaborazioniAgricole",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    AziendaId = table.Column<Guid>(type: "uuid", nullable: false),
                    Anno = table.Column<int>(type: "integer", nullable: false),
                    Mese = table.Column<int>(type: "integer", nullable: false),
                    Stato = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ValidataDa = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ValidataIl = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ElaborazioniAgricole", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ElaborazioniAgricole_TenantId_AziendaId_Anno_Mese",
                table: "ElaborazioniAgricole",
                columns: new[] { "TenantId", "AziendaId", "Anno", "Mese" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ElaborazioniAgricole");

            migrationBuilder.DropColumn(
                name: "RedditoPresuntoAnnuo",
                table: "Rapporti");

            migrationBuilder.DropColumn(
                name: "DeducibileIrpef",
                table: "ContributiAgricoli");

            migrationBuilder.DropColumn(
                name: "ArrotondamentoDelMese",
                table: "CedoliniAgricoli");

            migrationBuilder.DropColumn(
                name: "OrigineRedditoPresunto",
                table: "CedoliniAgricoli");

            migrationBuilder.DropColumn(
                name: "SegnalazioniJson",
                table: "CedoliniAgricoli");

            migrationBuilder.DropColumn(
                name: "RedditoPresuntoAgricoloAnnuo",
                table: "Aziende");
        }
    }
}
