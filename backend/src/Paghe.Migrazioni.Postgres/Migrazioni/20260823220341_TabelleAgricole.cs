using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Paghe.Migrazioni.Postgres.Migrazioni
{
    /// <inheritdoc />
    public partial class TabelleAgricole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ContributiAgricoli",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Anno = table.Column<int>(type: "integer", nullable: false),
                    Provincia = table.Column<string>(type: "text", nullable: false),
                    Codice = table.Column<string>(type: "text", nullable: false),
                    Descrizione = table.Column<string>(type: "text", nullable: false),
                    Aliquota = table.Column<decimal>(type: "numeric(8,5)", precision: 8, scale: 5, nullable: false),
                    ACaricoDipendente = table.Column<bool>(type: "boolean", nullable: false),
                    Ordine = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContributiAgricoli", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ParametriAgricoli",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Anno = table.Column<int>(type: "integer", nullable: false),
                    Provincia = table.Column<string>(type: "text", nullable: false),
                    AliquotaContributiDeducibili = table.Column<decimal>(type: "numeric(8,5)", precision: 8, scale: 5, nullable: false),
                    Fonte = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParametriAgricoli", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TariffeAgricole",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Anno = table.Column<int>(type: "integer", nullable: false),
                    Provincia = table.Column<string>(type: "text", nullable: false),
                    Livello = table.Column<string>(type: "text", nullable: false),
                    Descrizione = table.Column<string>(type: "text", nullable: false),
                    SalarioProvincialeOrario = table.Column<decimal>(type: "numeric(12,5)", precision: 12, scale: 5, nullable: false),
                    TerzoElementoOrario = table.Column<decimal>(type: "numeric(12,5)", precision: 12, scale: 5, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TariffeAgricole", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ContributiAgricoli_Anno_Provincia_Codice",
                table: "ContributiAgricoli",
                columns: new[] { "Anno", "Provincia", "Codice" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ParametriAgricoli_Anno_Provincia",
                table: "ParametriAgricoli",
                columns: new[] { "Anno", "Provincia" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TariffeAgricole_Anno_Provincia_Livello",
                table: "TariffeAgricole",
                columns: new[] { "Anno", "Provincia", "Livello" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContributiAgricoli");

            migrationBuilder.DropTable(
                name: "ParametriAgricoli");

            migrationBuilder.DropTable(
                name: "TariffeAgricole");
        }
    }
}
