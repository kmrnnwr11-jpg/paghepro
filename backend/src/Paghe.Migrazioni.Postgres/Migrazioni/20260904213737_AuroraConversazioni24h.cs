using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Paghe.Migrazioni.Postgres.Migrazioni
{
    /// <inheritdoc />
    public partial class AuroraConversazioni24h : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuroraConversazioni",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    UtenteId = table.Column<Guid>(type: "uuid", nullable: false),
                    TipoContesto = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    AziendaId = table.Column<Guid>(type: "uuid", nullable: true),
                    RapportoId = table.Column<Guid>(type: "uuid", nullable: true),
                    Periodo = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: true),
                    TitoloProtetto = table.Column<string>(type: "text", nullable: false),
                    ProgettoProtetto = table.Column<string>(type: "text", nullable: true),
                    CreataUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AggiornataUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ScadeUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ArchiviataUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Versione = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuroraConversazioni", x => x.Id);
                    table.UniqueConstraint("AK_AuroraConversazioni_TenantId_UtenteId_Id", x => new { x.TenantId, x.UtenteId, x.Id });
                });

            migrationBuilder.CreateTable(
                name: "AuroraCronologiaPreferenze",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    UtenteId = table.Column<Guid>(type: "uuid", nullable: false),
                    Attiva = table.Column<bool>(type: "boolean", nullable: false),
                    AggiornataUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Versione = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuroraCronologiaPreferenze", x => new { x.TenantId, x.UtenteId });
                });

            migrationBuilder.CreateTable(
                name: "AuroraTurni",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    UtenteId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConversazioneId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientTurnId = table.Column<Guid>(type: "uuid", nullable: false),
                    Stato = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CreatoUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletatoUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Versione = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuroraTurni", x => x.Id);
                    table.UniqueConstraint("AK_AuroraTurni_TenantId_UtenteId_ConversazioneId_Id", x => new { x.TenantId, x.UtenteId, x.ConversazioneId, x.Id });
                    table.ForeignKey(
                        name: "FK_AuroraTurni_AuroraConversazioni_TenantId_UtenteId_Conversaz~",
                        columns: x => new { x.TenantId, x.UtenteId, x.ConversazioneId },
                        principalTable: "AuroraConversazioni",
                        principalColumns: new[] { "TenantId", "UtenteId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AuroraMessaggi",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    UtenteId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConversazioneId = table.Column<Guid>(type: "uuid", nullable: false),
                    TurnoId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sequenza = table.Column<int>(type: "integer", nullable: false),
                    Ruolo = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    ContenutoProtetto = table.Column<string>(type: "text", nullable: false),
                    CreatoUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuroraMessaggi", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AuroraMessaggi_AuroraTurni_TenantId_UtenteId_ConversazioneI~",
                        columns: x => new { x.TenantId, x.UtenteId, x.ConversazioneId, x.TurnoId },
                        principalTable: "AuroraTurni",
                        principalColumns: new[] { "TenantId", "UtenteId", "ConversazioneId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuroraConversazioni_TenantId_UtenteId_AggiornataUtc",
                table: "AuroraConversazioni",
                columns: new[] { "TenantId", "UtenteId", "AggiornataUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AuroraConversazioni_TenantId_UtenteId_ScadeUtc",
                table: "AuroraConversazioni",
                columns: new[] { "TenantId", "UtenteId", "ScadeUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AuroraMessaggi_TenantId_UtenteId_ConversazioneId_Sequenza",
                table: "AuroraMessaggi",
                columns: new[] { "TenantId", "UtenteId", "ConversazioneId", "Sequenza" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuroraMessaggi_TenantId_UtenteId_ConversazioneId_TurnoId",
                table: "AuroraMessaggi",
                columns: new[] { "TenantId", "UtenteId", "ConversazioneId", "TurnoId" });

            migrationBuilder.CreateIndex(
                name: "IX_AuroraTurni_TenantId_UtenteId_ConversazioneId_ClientTurnId",
                table: "AuroraTurni",
                columns: new[] { "TenantId", "UtenteId", "ConversazioneId", "ClientTurnId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuroraCronologiaPreferenze");

            migrationBuilder.DropTable(
                name: "AuroraMessaggi");

            migrationBuilder.DropTable(
                name: "AuroraTurni");

            migrationBuilder.DropTable(
                name: "AuroraConversazioni");
        }
    }
}
