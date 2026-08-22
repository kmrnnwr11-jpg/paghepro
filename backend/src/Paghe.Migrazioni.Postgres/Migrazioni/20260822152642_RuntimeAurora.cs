using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Paghe.Migrazioni.Postgres.Migrazioni
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
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    UtenteId = table.Column<Guid>(type: "uuid", nullable: false),
                    GrantId = table.Column<Guid>(type: "uuid", nullable: true),
                    RunId = table.Column<Guid>(type: "uuid", nullable: true),
                    Strumento = table.Column<string>(type: "text", nullable: false),
                    ArgomentiProtetti = table.Column<string>(type: "text", nullable: false),
                    ArgomentiHash = table.Column<string>(type: "text", nullable: false),
                    Titolo = table.Column<string>(type: "text", nullable: false),
                    DiffJson = table.Column<string>(type: "text", nullable: false),
                    CreataUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ScadeUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Stato = table.Column<string>(type: "text", nullable: false),
                    ApplicataUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EsitoProtetto = table.Column<string>(type: "text", nullable: true),
                    Versione = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiProposte", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AiRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    UtenteId = table.Column<Guid>(type: "uuid", nullable: false),
                    GrantId = table.Column<Guid>(type: "uuid", nullable: true),
                    SezioneUi = table.Column<string>(type: "text", nullable: true),
                    Modello = table.Column<string>(type: "text", nullable: false),
                    InizioUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FineUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Giri = table.Column<int>(type: "integer", nullable: false),
                    TokenTotali = table.Column<int>(type: "integer", nullable: true),
                    Esito = table.Column<string>(type: "text", nullable: false),
                    Motivo = table.Column<string>(type: "text", nullable: true),
                    DigestMessaggi = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AiToolCalls",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    RunId = table.Column<Guid>(type: "uuid", nullable: false),
                    Ordine = table.Column<int>(type: "integer", nullable: false),
                    Strumento = table.Column<string>(type: "text", nullable: false),
                    ArgomentiHash = table.Column<string>(type: "text", nullable: false),
                    Esito = table.Column<string>(type: "text", nullable: false),
                    Motivo = table.Column<string>(type: "text", nullable: true),
                    DurataMs = table.Column<int>(type: "integer", nullable: false),
                    Righe = table.Column<int>(type: "integer", nullable: false)
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
