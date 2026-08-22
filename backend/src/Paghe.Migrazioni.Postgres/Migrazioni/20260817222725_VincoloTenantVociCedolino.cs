using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Paghe.Migrazioni.Postgres.Migrazioni
{
    /// <inheritdoc />
    public partial class VincoloTenantVociCedolino : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Le versioni legacy usavano Guid.Empty finché il tenant non veniva dedotto dal
            // cedolino. Ripariamo soltanto la sentinella ancora priva di payload: un cifrato AEAD
            // è legato al TenantId originale e non deve diventare illeggibile per una riscrittura.
            migrationBuilder.Sql(
                """
                UPDATE "CedolinoVoceEntity" AS v
                SET "TenantId" = c."TenantId"
                FROM "Cedolini" AS c
                WHERE c."Id" = v."CedolinoId"
                  AND v."TenantId" = '00000000-0000-0000-0000-000000000000'
                  AND BTRIM(COALESCE(v."DatiVoceProtetti", '')) = '';
                """);

            migrationBuilder.Sql(
                """
                DO $paghe$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM "CedolinoVoceEntity" AS v
                        LEFT JOIN "Cedolini" AS c ON c."Id" = v."CedolinoId"
                        WHERE c."Id" IS NULL OR v."TenantId" <> c."TenantId") THEN
                        RAISE EXCEPTION
                            'Voci cedolino orfane o con TenantId discordante: riallineamento automatico non sicuro per dati AEAD';
                    END IF;
                END
                $paghe$;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_CedolinoVoceEntity_Cedolini_CedolinoId",
                table: "CedolinoVoceEntity");

            migrationBuilder.DropIndex(
                name: "IX_CedolinoVoceEntity_CedolinoId",
                table: "CedolinoVoceEntity");

            migrationBuilder.AddForeignKey(
                name: "FK_CedolinoVoceEntity_Cedolini_TenantId_CedolinoId",
                table: "CedolinoVoceEntity",
                columns: new[] { "TenantId", "CedolinoId" },
                principalTable: "Cedolini",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CedolinoVoceEntity_Cedolini_TenantId_CedolinoId",
                table: "CedolinoVoceEntity");

            migrationBuilder.CreateIndex(
                name: "IX_CedolinoVoceEntity_CedolinoId",
                table: "CedolinoVoceEntity",
                column: "CedolinoId");

            migrationBuilder.AddForeignKey(
                name: "FK_CedolinoVoceEntity_Cedolini_CedolinoId",
                table: "CedolinoVoceEntity",
                column: "CedolinoId",
                principalTable: "Cedolini",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
