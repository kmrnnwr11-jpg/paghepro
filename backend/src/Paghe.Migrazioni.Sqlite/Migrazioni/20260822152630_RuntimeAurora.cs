using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Paghe.Migrazioni.Sqlite.Migrazioni
{
    /// <inheritdoc />
    public partial class RuntimeAurora : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AiProposte",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UtenteId = table.Column<Guid>(type: "TEXT", nullable: false),
                    GrantId = table.Column<Guid>(type: "TEXT", nullable: true),
                    RunId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Strumento = table.Column<string>(type: "TEXT", nullable: false),
                    ArgomentiProtetti = table.Column<string>(type: "TEXT", nullable: false),
                    ArgomentiHash = table.Column<string>(type: "TEXT", nullable: false),
                    Titolo = table.Column<string>(type: "TEXT", nullable: false),
                    DiffJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreataUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ScadeUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Stato = table.Column<string>(type: "TEXT", nullable: false),
                    ApplicataUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    EsitoProtetto = table.Column<string>(type: "TEXT", nullable: true),
                    Versione = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiProposte", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AiRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UtenteId = table.Column<Guid>(type: "TEXT", nullable: false),
                    GrantId = table.Column<Guid>(type: "TEXT", nullable: true),
                    SezioneUi = table.Column<string>(type: "TEXT", nullable: true),
                    Modello = table.Column<string>(type: "TEXT", nullable: false),
                    InizioUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FineUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Giri = table.Column<int>(type: "INTEGER", nullable: false),
                    TokenTotali = table.Column<int>(type: "INTEGER", nullable: true),
                    Esito = table.Column<string>(type: "TEXT", nullable: false),
                    Motivo = table.Column<string>(type: "TEXT", nullable: true),
                    DigestMessaggi = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AiToolCalls",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RunId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Ordine = table.Column<int>(type: "INTEGER", nullable: false),
                    Strumento = table.Column<string>(type: "TEXT", nullable: false),
                    ArgomentiHash = table.Column<string>(type: "TEXT", nullable: false),
                    Esito = table.Column<string>(type: "TEXT", nullable: false),
                    Motivo = table.Column<string>(type: "TEXT", nullable: true),
                    DurataMs = table.Column<int>(type: "INTEGER", nullable: false),
                    Righe = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiToolCalls", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AiProposte_TenantId_Stato",
                table: "AiProposte",
                columns: new[] { "TenantId", "Stato" });

            migrationBuilder.CreateIndex(
                name: "IX_AiRuns_TenantId_InizioUtc",
                table: "AiRuns",
                columns: new[] { "TenantId", "InizioUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AiToolCalls_TenantId_RunId_Ordine",
                table: "AiToolCalls",
                columns: new[] { "TenantId", "RunId", "Ordine" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiProposte");

            migrationBuilder.DropTable(
                name: "AiRuns");

            migrationBuilder.DropTable(
                name: "AiToolCalls");
        }
    }
}
