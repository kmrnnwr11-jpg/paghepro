using Microsoft.EntityFrameworkCore;

namespace Paghe.Persistence.Sicurezza;

/// <summary>Crea la tabella sessioni anche sui database evoluti con EnsureCreated.</summary>
public static class SessioniRefreshSchema
{
    public static void EnsureTabelle(PagheDbContext db, bool postgres) =>
        db.Database.ExecuteSqlRaw(postgres ? DdlPostgres : DdlSqlite);

    private const string DdlSqlite = """
        CREATE TABLE IF NOT EXISTS "SessioniRefresh" (
            "Id" TEXT NOT NULL CONSTRAINT "PK_SessioniRefresh" PRIMARY KEY,
            "TenantId" TEXT NOT NULL,
            "UtenteId" TEXT NOT NULL,
            "FamigliaId" TEXT NOT NULL,
            "HashSegreto" TEXT NOT NULL,
            "Client" TEXT NOT NULL,
            "CreataUtc" TEXT NOT NULL,
            "ScadeAssolutaUtc" TEXT NOT NULL,
            "ScadeInattivitaUtc" TEXT NOT NULL,
            "UsataUtc" TEXT NULL,
            "RevocataUtc" TEXT NULL,
            "MotivoRevoca" TEXT NULL,
            "SostituitaDaId" TEXT NULL,
            "Versione" INTEGER NOT NULL,
            CONSTRAINT "FK_SessioniRefresh_Utenti_UtenteId"
                FOREIGN KEY ("UtenteId") REFERENCES "Utenti" ("Id") ON DELETE CASCADE,
            CONSTRAINT "FK_SessioniRefresh_Tenants_TenantId"
                FOREIGN KEY ("TenantId") REFERENCES "Tenants" ("Id") ON DELETE CASCADE,
            CONSTRAINT "FK_SessioniRefresh_SessioniRefresh_SostituitaDaId"
                FOREIGN KEY ("SostituitaDaId") REFERENCES "SessioniRefresh" ("Id") ON DELETE RESTRICT
        );
        CREATE UNIQUE INDEX IF NOT EXISTS "IX_SessioniRefresh_TenantId_Id"
            ON "SessioniRefresh" ("TenantId", "Id");
        CREATE INDEX IF NOT EXISTS "IX_SessioniRefresh_TenantId_FamigliaId"
            ON "SessioniRefresh" ("TenantId", "FamigliaId");
        CREATE INDEX IF NOT EXISTS "IX_SessioniRefresh_TenantId_UtenteId_RevocataUtc"
            ON "SessioniRefresh" ("TenantId", "UtenteId", "RevocataUtc");
        """;

    private const string DdlPostgres = """
        CREATE TABLE IF NOT EXISTS "SessioniRefresh" (
            "Id" uuid NOT NULL CONSTRAINT "PK_SessioniRefresh" PRIMARY KEY,
            "TenantId" uuid NOT NULL,
            "UtenteId" uuid NOT NULL,
            "FamigliaId" uuid NOT NULL,
            "HashSegreto" text NOT NULL,
            "Client" text NOT NULL,
            "CreataUtc" timestamp with time zone NOT NULL,
            "ScadeAssolutaUtc" timestamp with time zone NOT NULL,
            "ScadeInattivitaUtc" timestamp with time zone NOT NULL,
            "UsataUtc" timestamp with time zone NULL,
            "RevocataUtc" timestamp with time zone NULL,
            "MotivoRevoca" text NULL,
            "SostituitaDaId" uuid NULL,
            "Versione" bigint NOT NULL,
            CONSTRAINT "FK_SessioniRefresh_Utenti_UtenteId"
                FOREIGN KEY ("UtenteId") REFERENCES "Utenti" ("Id") ON DELETE CASCADE,
            CONSTRAINT "FK_SessioniRefresh_Tenants_TenantId"
                FOREIGN KEY ("TenantId") REFERENCES "Tenants" ("Id") ON DELETE CASCADE,
            CONSTRAINT "FK_SessioniRefresh_SessioniRefresh_SostituitaDaId"
                FOREIGN KEY ("SostituitaDaId") REFERENCES "SessioniRefresh" ("Id") ON DELETE RESTRICT
        );
        CREATE UNIQUE INDEX IF NOT EXISTS "IX_SessioniRefresh_TenantId_Id"
            ON "SessioniRefresh" ("TenantId", "Id");
        CREATE INDEX IF NOT EXISTS "IX_SessioniRefresh_TenantId_FamigliaId"
            ON "SessioniRefresh" ("TenantId", "FamigliaId");
        CREATE INDEX IF NOT EXISTS "IX_SessioniRefresh_TenantId_UtenteId_RevocataUtc"
            ON "SessioniRefresh" ("TenantId", "UtenteId", "RevocataUtc");
        """;
}
