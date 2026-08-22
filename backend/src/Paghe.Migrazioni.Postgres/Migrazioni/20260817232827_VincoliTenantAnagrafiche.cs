using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Paghe.Migrazioni.Postgres.Migrazioni
{
    /// <inheritdoc />
    public partial class VincoliTenantAnagrafiche : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Nessun riallineamento silenzioso: in caso di dati tenant-discordanti l'intera
            // migrazione transazionale fallisce prima di sostituire i vincoli legacy.
            migrationBuilder.Sql(
                """
                DO $paghe$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM "Rapporti" AS r
                        LEFT JOIN "Dipendenti" AS d ON d."Id" = r."DipendenteId"
                        WHERE d."Id" IS NULL OR d."TenantId" <> r."TenantId")
                       OR EXISTS (
                        SELECT 1
                        FROM "Durc" AS u
                        LEFT JOIN "Aziende" AS a ON a."Id" = u."AziendaId"
                        WHERE a."Id" IS NULL OR a."TenantId" <> u."TenantId")
                       OR EXISTS (
                        SELECT 1
                        FROM "Promemoria" AS p
                        LEFT JOIN "Aziende" AS a ON a."Id" = p."AziendaId"
                        WHERE p."AziendaId" IS NOT NULL
                          AND (a."Id" IS NULL OR a."TenantId" <> p."TenantId")) THEN
                        RAISE EXCEPTION
                            'Vincoli tenant anagrafici: rapporti, DURC o promemoria orfani/discordanti';
                    END IF;
                END
                $paghe$;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_Durc_Aziende_AziendaId",
                table: "Durc");

            migrationBuilder.DropForeignKey(
                name: "FK_Promemoria_Aziende_AziendaId",
                table: "Promemoria");

            migrationBuilder.DropForeignKey(
                name: "FK_Rapporti_Dipendenti_DipendenteId",
                table: "Rapporti");

            migrationBuilder.DropIndex(
                name: "IX_Rapporti_DipendenteId",
                table: "Rapporti");

            migrationBuilder.DropIndex(
                name: "IX_Promemoria_AziendaId",
                table: "Promemoria");

            migrationBuilder.DropIndex(
                name: "IX_Durc_AziendaId",
                table: "Durc");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Dipendenti_TenantId_Id",
                table: "Dipendenti",
                columns: new[] { "TenantId", "Id" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Aziende_TenantId_Id",
                table: "Aziende",
                columns: new[] { "TenantId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Rapporti_TenantId_DipendenteId",
                table: "Rapporti",
                columns: new[] { "TenantId", "DipendenteId" });

            migrationBuilder.CreateIndex(
                name: "IX_Promemoria_TenantId_AziendaId",
                table: "Promemoria",
                columns: new[] { "TenantId", "AziendaId" });

            migrationBuilder.AlterColumn<decimal>(
                name: "FerieResidueIniziali",
                table: "Rapporti",
                type: "numeric(15,2)",
                precision: 15,
                scale: 2,
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "numeric(15,2)",
                oldPrecision: 15,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "PermessiResiduiIniziali",
                table: "Rapporti",
                type: "numeric(15,2)",
                precision: 15,
                scale: 2,
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "numeric(15,2)",
                oldPrecision: 15,
                oldScale: 2);

            migrationBuilder.AddForeignKey(
                name: "FK_Durc_Aziende_TenantId_AziendaId",
                table: "Durc",
                columns: new[] { "TenantId", "AziendaId" },
                principalTable: "Aziende",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Promemoria_Aziende_TenantId_AziendaId",
                table: "Promemoria",
                columns: new[] { "TenantId", "AziendaId" },
                principalTable: "Aziende",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Rapporti_Dipendenti_TenantId_DipendenteId",
                table: "Rapporti",
                columns: new[] { "TenantId", "DipendenteId" },
                principalTable: "Dipendenti",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Durc_Aziende_TenantId_AziendaId",
                table: "Durc");

            migrationBuilder.DropForeignKey(
                name: "FK_Promemoria_Aziende_TenantId_AziendaId",
                table: "Promemoria");

            migrationBuilder.DropForeignKey(
                name: "FK_Rapporti_Dipendenti_TenantId_DipendenteId",
                table: "Rapporti");

            migrationBuilder.DropIndex(
                name: "IX_Rapporti_TenantId_DipendenteId",
                table: "Rapporti");

            migrationBuilder.DropIndex(
                name: "IX_Promemoria_TenantId_AziendaId",
                table: "Promemoria");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Dipendenti_TenantId_Id",
                table: "Dipendenti");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Aziende_TenantId_Id",
                table: "Aziende");

            migrationBuilder.CreateIndex(
                name: "IX_Rapporti_DipendenteId",
                table: "Rapporti",
                column: "DipendenteId");

            migrationBuilder.CreateIndex(
                name: "IX_Promemoria_AziendaId",
                table: "Promemoria",
                column: "AziendaId");

            migrationBuilder.CreateIndex(
                name: "IX_Durc_AziendaId",
                table: "Durc",
                column: "AziendaId");

            migrationBuilder.AddForeignKey(
                name: "FK_Durc_Aziende_AziendaId",
                table: "Durc",
                column: "AziendaId",
                principalTable: "Aziende",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Promemoria_Aziende_AziendaId",
                table: "Promemoria",
                column: "AziendaId",
                principalTable: "Aziende",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Rapporti_Dipendenti_DipendenteId",
                table: "Rapporti",
                column: "DipendenteId",
                principalTable: "Dipendenti",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
