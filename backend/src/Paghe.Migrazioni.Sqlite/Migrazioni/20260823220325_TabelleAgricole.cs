using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Paghe.Migrazioni.Sqlite.Migrazioni
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
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Anno = table.Column<int>(type: "INTEGER", nullable: false),
                    Provincia = table.Column<string>(type: "TEXT", nullable: false),
                    Codice = table.Column<string>(type: "TEXT", nullable: false),
                    Descrizione = table.Column<string>(type: "TEXT", nullable: false),
                    Aliquota = table.Column<decimal>(type: "TEXT", precision: 8, scale: 5, nullable: false),
                    ACaricoDipendente = table.Column<bool>(type: "INTEGER", nullable: false),
                    Ordine = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContributiAgricoli", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ParametriAgricoli",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Anno = table.Column<int>(type: "INTEGER", nullable: false),
                    Provincia = table.Column<string>(type: "TEXT", nullable: false),
                    AliquotaContributiDeducibili = table.Column<decimal>(type: "TEXT", precision: 8, scale: 5, nullable: false),
                    Fonte = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParametriAgricoli", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TariffeAgricole",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Anno = table.Column<int>(type: "INTEGER", nullable: false),
                    Provincia = table.Column<string>(type: "TEXT", nullable: false),
                    Livello = table.Column<string>(type: "TEXT", nullable: false),
                    Descrizione = table.Column<string>(type: "TEXT", nullable: false),
                    SalarioProvincialeOrario = table.Column<decimal>(type: "TEXT", precision: 12, scale: 5, nullable: false),
                    TerzoElementoOrario = table.Column<decimal>(type: "TEXT", precision: 12, scale: 5, nullable: false)
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
