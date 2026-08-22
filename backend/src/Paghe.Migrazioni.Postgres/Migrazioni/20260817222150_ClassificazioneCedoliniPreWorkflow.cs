using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Paghe.Migrazioni.Postgres.Migrazioni
{
    /// <inheritdoc />
    public partial class ClassificazioneCedoliniPreWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Migrazione correttiva append-only: alcuni database erano stati adottati quando la
            // migrazione del workflow risultava gia registrata, ma le righe legacy conservavano i
            // default neutri Stato=""/Versione=0. Le sentinelle congiunte distinguono quelle righe
            // dalle bozze correnti, che hanno fotografia e hash dei parametri applicati.
            migrationBuilder.Sql(
                """
                UPDATE "Cedolini"
                SET "Stato" = 'PreWorkflow',
                    "Versione" = 1
                WHERE (
                        (TRIM(COALESCE("Stato", '')) = '' AND "Versione" IN (0, 1))
                        OR (TRIM(COALESCE("Stato", '')) = 'Bozza' AND "Versione" = 1)
                      )
                  AND TRIM(COALESCE("HashParametriSha256", '')) = ''
                  AND TRIM(COALESCE("ParametriApplicatiProtetti", '')) = ''
                  AND "ControllatoIl" IS NULL
                  AND "ControllatoDa" IS NULL
                  AND "ValidatoIl" IS NULL
                  AND "ValidatoDa" IS NULL
                  AND TRIM(COALESCE("DatiValidazioneProtetti", '')) = ''
                  AND "ChiusoIl" IS NULL
                  AND "ChiusoDa" IS NULL
                  AND NOT EXISTS (
                      SELECT 1
                      FROM "CedoliniSnapshot" AS s
                      WHERE s."TenantId" = "Cedolini"."TenantId"
                        AND s."CedolinoId" = "Cedolini"."Id")
                  AND NOT EXISTS (
                      SELECT 1
                      FROM "CedoliniDocumenti" AS d
                      WHERE d."TenantId" = "Cedolini"."TenantId"
                        AND d."CedolinoId" = "Cedolini"."Id");
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Non reversibile in modo affidabile: dopo la classificazione non e possibile
            // distinguere una riga legacy da uno stato PreWorkflow gia legittimo.
        }
    }
}
