using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Paghe.Migrazioni.Postgres.Migrazioni
{
    /// <inheritdoc />
    public partial class ArchivioBusteAgricole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CedoliniAgricoli",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    AziendaId = table.Column<Guid>(type: "uuid", nullable: false),
                    RapportoLavoroId = table.Column<Guid>(type: "uuid", nullable: false),
                    Anno = table.Column<int>(type: "integer", nullable: false),
                    Mese = table.Column<int>(type: "integer", nullable: false),
                    OreOrdinarie = table.Column<decimal>(type: "numeric(15,2)", precision: 15, scale: 2, nullable: false),
                    GiornateLavorate = table.Column<int>(type: "integer", nullable: false),
                    GiorniDetrazione = table.Column<int>(type: "integer", nullable: false),
                    UnaTantum = table.Column<decimal>(type: "numeric(15,2)", precision: 15, scale: 2, nullable: false),
                    GiorniTrasferta = table.Column<decimal>(type: "numeric(15,2)", precision: 15, scale: 2, nullable: false),
                    IndennitaTrasfertaGiornaliera = table.Column<decimal>(type: "numeric(15,2)", precision: 15, scale: 2, nullable: false),
                    RimborsiSpeseDocumentate = table.Column<decimal>(type: "numeric(15,2)", precision: 15, scale: 2, nullable: false),
                    DetrazioniGiaGoduteAnno = table.Column<decimal>(type: "numeric(15,2)", precision: 15, scale: 2, nullable: false),
                    ArrotondamentoRiportato = table.Column<decimal>(type: "numeric(15,2)", precision: 15, scale: 2, nullable: false),
                    UltimoCedolino = table.Column<bool>(type: "boolean", nullable: false),
                    RedditoPresuntoAnnuo = table.Column<decimal>(type: "numeric(15,2)", precision: 15, scale: 2, nullable: false),
                    TotaleCompetenze = table.Column<decimal>(type: "numeric(15,2)", precision: 15, scale: 2, nullable: false),
                    TotaleTrattenute = table.Column<decimal>(type: "numeric(15,2)", precision: 15, scale: 2, nullable: false),
                    ImponibileInps = table.Column<decimal>(type: "numeric(15,2)", precision: 15, scale: 2, nullable: false),
                    ContributiDipendente = table.Column<decimal>(type: "numeric(15,2)", precision: 15, scale: 2, nullable: false),
                    ImponibileIrpef = table.Column<decimal>(type: "numeric(15,2)", precision: 15, scale: 2, nullable: false),
                    IrpefNetta = table.Column<decimal>(type: "numeric(15,2)", precision: 15, scale: 2, nullable: false),
                    NettoInBusta = table.Column<decimal>(type: "numeric(15,2)", precision: 15, scale: 2, nullable: false),
                    ProvinciaCpl = table.Column<string>(type: "text", nullable: false),
                    LivelloTariffa = table.Column<string>(type: "text", nullable: false),
                    FonteTabelle = table.Column<string>(type: "text", nullable: false),
                    PdfProtetto = table.Column<string>(type: "text", nullable: false),
                    HashPdfSha256 = table.Column<string>(type: "text", nullable: false),
                    ArchiviatoIl = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ArchiviatoDa = table.Column<Guid>(type: "uuid", nullable: false),
                    Versione = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CedoliniAgricoli", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CedoliniAgricoli_TenantId_AziendaId_Anno_Mese",
                table: "CedoliniAgricoli",
                columns: new[] { "TenantId", "AziendaId", "Anno", "Mese" });

            migrationBuilder.CreateIndex(
                name: "IX_CedoliniAgricoli_TenantId_RapportoLavoroId_Anno_Mese",
                table: "CedoliniAgricoli",
                columns: new[] { "TenantId", "RapportoLavoroId", "Anno", "Mese" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CedoliniAgricoli");
        }
    }
}
