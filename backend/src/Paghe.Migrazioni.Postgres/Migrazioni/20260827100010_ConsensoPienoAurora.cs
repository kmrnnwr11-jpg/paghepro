using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Paghe.Persistence;

#nullable disable

namespace Paghe.Migrazioni.Postgres.Migrazioni
{
    /// <summary>
    /// Scritta a mano (la CLI `dotnet ef` non era disponibile in questa sessione): gli
    /// attributi che di solito stanno nel file .Designer sono qui sulla classe, e non c'è
    /// BuildTargetModel — per una migrazione di solo CreateTable il modello di destinazione
    /// non viene usato. Lo snapshot del modello è stato aggiornato insieme a questo file;
    /// la prossima migrazione va generata normalmente con la CLI.
    /// </summary>
    [DbContext(typeof(PagheDbContext))]
    [Migration("20260827100010_ConsensoPienoAurora")]
    public partial class ConsensoPienoAurora : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConsensiAurora",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    UtenteId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConcessoUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RevocatoUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConsensiAurora", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConsensiAurora_TenantId_UtenteId",
                table: "ConsensiAurora",
                columns: new[] { "TenantId", "UtenteId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConsensiAurora");
        }
    }
}
