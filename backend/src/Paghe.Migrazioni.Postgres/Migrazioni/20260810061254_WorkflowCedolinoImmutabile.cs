using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Paghe.Migrazioni.Postgres.Migrazioni
{
    /// <inheritdoc />
    public partial class WorkflowCedolinoImmutabile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ChiusoDa",
                table: "Cedolini",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ChiusoIl",
                table: "Cedolini",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CodiceCnel",
                table: "Cedolini",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ControllatoDa",
                table: "Cedolini",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ControllatoIl",
                table: "Cedolini",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DatiValidazioneProtetti",
                table: "Cedolini",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DescrizioneCcnl",
                table: "Cedolini",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "DetrazioniLavoroDipendente",
                table: "Cedolini",
                type: "numeric(15,2)",
                precision: 15,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "FonteCcnl",
                table: "Cedolini",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HashParametriSha256",
                table: "Cedolini",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "ImpostaSostitutivaPremio",
                table: "Cedolini",
                type: "numeric(15,2)",
                precision: 15,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "IrpefLorda",
                table: "Cedolini",
                type: "numeric(15,2)",
                precision: 15,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "ParametriApplicatiProtetti",
                table: "Cedolini",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "PremioRisultatoAgevolato",
                table: "Cedolini",
                type: "numeric(15,2)",
                precision: 15,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "Stato",
                table: "Cedolini",
                type: "text",
                nullable: false,
                defaultValue: "Bozza");

            migrationBuilder.AddColumn<decimal>(
                name: "TotaleEsenti",
                table: "Cedolini",
                type: "numeric(15,2)",
                precision: 15,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "ValidatoDa",
                table: "Cedolini",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ValidatoIl",
                table: "Cedolini",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Versione",
                table: "Cedolini",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            // Le righe presenti prima del workflow non hanno mai superato controllo, validazione
            // e chiusura. Uno stato distinto evita sia di certificarle implicitamente sia di
            // nasconderle ai riepiloghi storici. Il default della colonna resta "Bozza", quindi
            // ogni cedolino inserito dopo questa migrazione parte dal normale ciclo operativo.
            migrationBuilder.Sql(
                """
                UPDATE "Cedolini"
                SET "Stato" = 'PreWorkflow'
                WHERE "Stato" = 'Bozza'
                  AND "Versione" = 1
                  AND TRIM(COALESCE("HashParametriSha256", '')) = ''
                  AND TRIM(COALESCE("ParametriApplicatiProtetti", '')) = ''
                  AND "ControllatoIl" IS NULL
                  AND "ControllatoDa" IS NULL
                  AND "ValidatoIl" IS NULL
                  AND "ValidatoDa" IS NULL
                  AND TRIM(COALESCE("DatiValidazioneProtetti", '')) = ''
                  AND "ChiusoIl" IS NULL
                  AND "ChiusoDa" IS NULL;
                """);

            migrationBuilder.AddColumn<string>(
                name: "FirmaSiglaTimbroProtetta",
                table: "Aziende",
                type: "text",
                nullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Cedolini_TenantId_Id",
                table: "Cedolini",
                columns: new[] { "TenantId", "Id" });

            migrationBuilder.CreateTable(
                name: "CcnlVersioni",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CcnlCodice = table.Column<string>(type: "text", nullable: false),
                    Anno = table.Column<int>(type: "integer", nullable: false),
                    CodiceCnel = table.Column<string>(type: "text", nullable: true),
                    Fonte = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CcnlVersioni", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CcnlVersioni_Ccnl_CcnlCodice",
                        column: x => x.CcnlCodice,
                        principalTable: "Ccnl",
                        principalColumn: "Codice",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CedoliniDocumenti",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CedolinoId = table.Column<Guid>(type: "uuid", nullable: false),
                    PdfProtetto = table.Column<string>(type: "text", nullable: false),
                    HashPdfSha256 = table.Column<string>(type: "text", nullable: false),
                    HashFirmaSiglaTimbroSha256 = table.Column<string>(type: "text", nullable: false),
                    VersioneRenderer = table.Column<string>(type: "text", nullable: false),
                    CreatoIl = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatoDa = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CedoliniDocumenti", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CedoliniDocumenti_Cedolini_TenantId_CedolinoId",
                        columns: x => new { x.TenantId, x.CedolinoId },
                        principalTable: "Cedolini",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CedoliniSnapshot",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CedolinoId = table.Column<Guid>(type: "uuid", nullable: false),
                    VersioneSchema = table.Column<int>(type: "integer", nullable: false),
                    DatiProtetti = table.Column<string>(type: "text", nullable: false),
                    HashDatiSha256 = table.Column<string>(type: "text", nullable: false),
                    CreatoIl = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatoDa = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CedoliniSnapshot", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CedoliniSnapshot_Cedolini_TenantId_CedolinoId",
                        columns: x => new { x.TenantId, x.CedolinoId },
                        principalTable: "Cedolini",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CcnlVersioni_CcnlCodice_Anno",
                table: "CcnlVersioni",
                columns: new[] { "CcnlCodice", "Anno" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CedoliniDocumenti_TenantId_CedolinoId",
                table: "CedoliniDocumenti",
                columns: new[] { "TenantId", "CedolinoId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CedoliniSnapshot_TenantId_CedolinoId",
                table: "CedoliniSnapshot",
                columns: new[] { "TenantId", "CedolinoId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CcnlVersioni");

            migrationBuilder.DropTable(
                name: "CedoliniDocumenti");

            migrationBuilder.DropTable(
                name: "CedoliniSnapshot");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Cedolini_TenantId_Id",
                table: "Cedolini");

            migrationBuilder.DropColumn(
                name: "ChiusoDa",
                table: "Cedolini");

            migrationBuilder.DropColumn(
                name: "ChiusoIl",
                table: "Cedolini");

            migrationBuilder.DropColumn(
                name: "CodiceCnel",
                table: "Cedolini");

            migrationBuilder.DropColumn(
                name: "ControllatoDa",
                table: "Cedolini");

            migrationBuilder.DropColumn(
                name: "ControllatoIl",
                table: "Cedolini");

            migrationBuilder.DropColumn(
                name: "DatiValidazioneProtetti",
                table: "Cedolini");

            migrationBuilder.DropColumn(
                name: "DescrizioneCcnl",
                table: "Cedolini");

            migrationBuilder.DropColumn(
                name: "DetrazioniLavoroDipendente",
                table: "Cedolini");

            migrationBuilder.DropColumn(
                name: "FonteCcnl",
                table: "Cedolini");

            migrationBuilder.DropColumn(
                name: "HashParametriSha256",
                table: "Cedolini");

            migrationBuilder.DropColumn(
                name: "ImpostaSostitutivaPremio",
                table: "Cedolini");

            migrationBuilder.DropColumn(
                name: "IrpefLorda",
                table: "Cedolini");

            migrationBuilder.DropColumn(
                name: "ParametriApplicatiProtetti",
                table: "Cedolini");

            migrationBuilder.DropColumn(
                name: "PremioRisultatoAgevolato",
                table: "Cedolini");

            migrationBuilder.DropColumn(
                name: "Stato",
                table: "Cedolini");

            migrationBuilder.DropColumn(
                name: "TotaleEsenti",
                table: "Cedolini");

            migrationBuilder.DropColumn(
                name: "ValidatoDa",
                table: "Cedolini");

            migrationBuilder.DropColumn(
                name: "ValidatoIl",
                table: "Cedolini");

            migrationBuilder.DropColumn(
                name: "Versione",
                table: "Cedolini");

            migrationBuilder.DropColumn(
                name: "FirmaSiglaTimbroProtetta",
                table: "Aziende");
        }
    }
}
