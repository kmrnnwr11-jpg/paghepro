using Microsoft.EntityFrameworkCore;

namespace Paghe.Persistence.Ai;

/// <summary>
/// Rete di sicurezza per i database creati con EnsureCreated ed evoluti nel tempo: crea le
/// tabelle del Governor se mancanti, su SQLite e PostgreSQL (stesso pattern delle altre
/// tabelle recenti in Program.cs / EnsureSchemaSviluppoCompatibile, tenuto in un file
/// separato per isolare il modulo AI).
/// </summary>
public static class AiGovernorSchema
{
    public static void EnsureTabelle(PagheDbContext db, bool postgres)
    {
        db.Database.ExecuteSqlRaw(postgres ? DdlPostgres : DdlSqlite);
    }

    private const string DdlSqlite = """
        CREATE TABLE IF NOT EXISTS "AiScopeGrants" (
            "Id" TEXT NOT NULL CONSTRAINT "PK_AiScopeGrants" PRIMARY KEY,
            "TenantId" TEXT NOT NULL,
            "UtenteId" TEXT NOT NULL,
            "PurposeCode" TEXT NOT NULL,
            "Modalita" TEXT NOT NULL,
            "ScopeLevel" TEXT NOT NULL,
            "AziendaId" TEXT NULL,
            "Periodo" TEXT NULL,
            "RisorseAmmesse" TEXT NOT NULL,
            "OperazioniAmmesse" TEXT NOT NULL,
            "AutonomiaMax" TEXT NOT NULL,
            "PolicyVersione" TEXT NOT NULL,
            "Nonce" TEXT NOT NULL,
            "CreatoUtc" TEXT NOT NULL,
            "ScadeUtc" TEXT NOT NULL,
            "TimeoutInattivitaMinuti" INTEGER NOT NULL,
            "UltimoUsoUtc" TEXT NOT NULL,
            "MaxLetture" INTEGER NOT NULL,
            "LettureUsate" INTEGER NOT NULL,
            "Stato" TEXT NOT NULL,
            "RevocatoUtc" TEXT NULL,
            "Versione" INTEGER NOT NULL
        );
        CREATE INDEX IF NOT EXISTS "IX_AiScopeGrants_TenantId_Stato"
            ON "AiScopeGrants" ("TenantId", "Stato");
        CREATE TABLE IF NOT EXISTS "AiAudit" (
            "Id" TEXT NOT NULL CONSTRAINT "PK_AiAudit" PRIMARY KEY,
            "TenantId" TEXT NOT NULL,
            "Sequenza" INTEGER NOT NULL,
            "GrantId" TEXT NULL,
            "UtenteId" TEXT NULL,
            "Evento" TEXT NOT NULL,
            "Dettaglio" TEXT NOT NULL,
            "Motivo" TEXT NULL,
            "QuandoUtc" TEXT NOT NULL,
            "HashPrecedente" TEXT NOT NULL,
            "Hash" TEXT NOT NULL
        );
        CREATE UNIQUE INDEX IF NOT EXISTS "IX_AiAudit_TenantId_Sequenza"
            ON "AiAudit" ("TenantId", "Sequenza");
        """;

    private const string DdlPostgres = """
        CREATE TABLE IF NOT EXISTS "AiScopeGrants" (
            "Id" uuid NOT NULL CONSTRAINT "PK_AiScopeGrants" PRIMARY KEY,
            "TenantId" uuid NOT NULL,
            "UtenteId" uuid NOT NULL,
            "PurposeCode" text NOT NULL,
            "Modalita" text NOT NULL,
            "ScopeLevel" text NOT NULL,
            "AziendaId" uuid NULL,
            "Periodo" text NULL,
            "RisorseAmmesse" text NOT NULL,
            "OperazioniAmmesse" text NOT NULL,
            "AutonomiaMax" text NOT NULL,
            "PolicyVersione" text NOT NULL,
            "Nonce" text NOT NULL,
            "CreatoUtc" timestamp with time zone NOT NULL,
            "ScadeUtc" timestamp with time zone NOT NULL,
            "TimeoutInattivitaMinuti" integer NOT NULL,
            "UltimoUsoUtc" timestamp with time zone NOT NULL,
            "MaxLetture" integer NOT NULL,
            "LettureUsate" integer NOT NULL,
            "Stato" text NOT NULL,
            "RevocatoUtc" timestamp with time zone NULL,
            "Versione" integer NOT NULL
        );
        CREATE INDEX IF NOT EXISTS "IX_AiScopeGrants_TenantId_Stato"
            ON "AiScopeGrants" ("TenantId", "Stato");
        CREATE TABLE IF NOT EXISTS "AiAudit" (
            "Id" uuid NOT NULL CONSTRAINT "PK_AiAudit" PRIMARY KEY,
            "TenantId" uuid NOT NULL,
            "Sequenza" bigint NOT NULL,
            "GrantId" uuid NULL,
            "UtenteId" uuid NULL,
            "Evento" text NOT NULL,
            "Dettaglio" text NOT NULL,
            "Motivo" text NULL,
            "QuandoUtc" timestamp with time zone NOT NULL,
            "HashPrecedente" text NOT NULL,
            "Hash" text NOT NULL
        );
        CREATE UNIQUE INDEX IF NOT EXISTS "IX_AiAudit_TenantId_Sequenza"
            ON "AiAudit" ("TenantId", "Sequenza");
        """;
}
