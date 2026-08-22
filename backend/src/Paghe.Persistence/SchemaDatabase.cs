using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;

namespace Paghe.Persistence;

/// <summary>
/// Porta il database allo schema corrente usando le migrazioni EF versionate
/// (<c>Paghe.Migrazioni.Sqlite</c> / <c>Paghe.Migrazioni.Postgres</c>).
///
/// Casi gestiti:
/// <list type="number">
/// <item>database inesistente o senza tabelle dell'applicazione: le migrazioni creano tutto;</item>
/// <item>database già gestito dalle migrazioni: applica quelle mancanti;</item>
/// <item>database creato prima delle migrazioni (<c>EnsureCreated</c> + DDL scritto a mano):
/// viene <b>adottato</b> — colonne e tabelle mancanti completate dalla migrazione iniziale, che
/// viene poi registrata come già applicata. I cedolini precedenti al workflow vengono soltanto
/// classificati come tali, senza attribuire loro una validazione mai avvenuta.</item>
/// </list>
/// </summary>
public static class SchemaDatabase
{
    private const string TabellaAdozione = "__PagheAdozioneSchema";

    /// <summary>Applica lo schema e restituisce una descrizione di cosa è stato fatto.</summary>
    public static string Applica(PagheDbContext db)
    {
        // Database ancora inesistente (file SQLite o database PostgreSQL): lo crea Migrate().
        if (!db.Database.GetService<IRelationalDatabaseCreator>().Exists())
        {
            db.Database.Migrate();
            return "database creato dalle migrazioni EF";
        }

        var presenti = TabellePresenti(db);

        // Lo storico si verifica sull'elenco reale delle tabelle, non con
        // IHistoryRepository.Exists(): su Npgsql quest'ultimo risponde "vero" anche quando la
        // tabella non c'è, e Migrate() finirebbe per ricreare uno schema già popolato. Uno
        // storico presente ma VUOTO (residuo di un avvio fallito) vale come assente.
        if (!presenti.Contains(TabellaAdozione)
            && presenti.Contains(HistoryRepository.DefaultTableName)
            && db.Database.GetAppliedMigrations().Any())
        {
            var daApplicare = db.Database.GetPendingMigrations().ToList();
            // Alcune installazioni erano state allineate dal vecchio bootstrap aggiungendo le
            // colonne del modello corrente, ma conservavano uno storico fermo al workflow. In
            // quel caso Migrate() tenterebbe di aggiungerle una seconda volta. Riconciliamo solo
            // la migrazione strutturale nota e soltanto dopo averne provato l'equivalenza; le
            // migrazioni dati e i vincoli successivi vengono comunque eseguiti normalmente da EF.
            RiconciliaResiduiAnfGiaPresenti(db, daApplicare);
            if (daApplicare.Count > 0
                && daApplicare[0].EndsWith(
                    "_ResiduiRapportoEAssegniFamiliari",
                    StringComparison.Ordinal)
                && daApplicare.Any(m =>
                    m.EndsWith("_VincoloTenantVociCedolino", StringComparison.Ordinal)))
            {
                // Il vecchio bootstrap poteva aver registrato Workflow senza installarne la
                // chiave alternativa. La FK composita pending sarebbe allora formalmente creata
                // da SQLite ma inutilizzabile (foreign key mismatch). Il preflight duplicati e la
                // chiave candidata devono precedere Migrate().
                GarantisceChiaveTenantCedolino(db);
            }
            db.Database.Migrate();
            return daApplicare.Count == 0
                ? "schema già aggiornato"
                : $"applicate {daApplicare.Count} migrazioni: {string.Join(", ", daApplicare)}";
        }

        var attese = db.Model.GetEntityTypes()
            .Select(e => e.GetTableName())
            .Where(t => t is not null)
            .Select(t => t!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Nessuna tabella dell'applicazione: le migrazioni creano lo schema completo. Il controllo
        // è sulle tabelle del modello, non sul database: Supabase arriva con i propri schemi
        // (auth, storage) e un database "non vuoto" non significa applicazione già installata.
        if (!attese.Any(presenti.Contains))
        {
            db.Database.Migrate();
            return "schema dell'applicazione creato dalle migrazioni EF";
        }

        var migrazioni = db.Database.GetMigrations().ToList();
        if (migrazioni.Count == 0)
            throw new InvalidOperationException(
                "Nessuna migrazione trovata nell'assembly configurato: verificare MigrationsAssembly " +
                "in Program.cs e il riferimento al progetto di migrazione.");

        // Una sentinella durevole distingue una vera adozione interrotta da uno storico parziale
        // arbitrario. Viene scritta prima del primo DDL e contiene la testa esatta verso cui si sta
        // convergendo; un riavvio può quindi ripetere in sicurezza i soli passaggi idempotenti.
        IniziaOVerificaAdozione(db, migrazioni[^1]);

        // Adozione di uno schema preesistente: lo si porta al modello CORRENTE (colonne, tabelle e
        // indici mancanti) e si registrano TUTTE le migrazioni come applicate.
        //
        // Registrare solo la iniziale non funziona: AllineaColonneMancanti confronta con il modello
        // corrente, quindi aggiunge già le colonne introdotte dalle migrazioni successive, e
        // Migrate() fallirebbe poi con "duplicate column". Lo schema è a livello della testa: va
        // dichiarato tale.
        db.AllineaColonneMancanti();
        // Le tabelle Snapshot/Documenti eventualmente mancanti hanno FK compositi verso questa
        // chiave: PostgreSQL richiede che esista prima dei CREATE TABLE generati da CompletaSchema.
        if (presenti.Contains("Cedolini")) GarantisceChiaveTenantCedolino(db);
        AllineaVincoloTenantVociCedolino(db);
        AllineaVincoliTenantAnagrafiche(db);
        var cedoliniPreWorkflow = ClassificaCedoliniPreWorkflow(db);
        var tabelleCreate = CompletaSchema(db, attese.Where(t => !presenti.Contains(t)).ToList());
        // Se CedolinoVoceEntity era assente, CompletaSchema l'ha ricreata dalla migrazione iniziale
        // con il vecchio FK sul solo CedolinoId e senza le colonne delle migrazioni successive
        // (per esempio Ordine). Il secondo allineamento completa prima le colonne e poi il vincolo,
        // obbligatoriamente prima di dichiarare applicata l'intera catena nello storico.
        db.AllineaColonneMancanti();
        AllineaVincoloTenantVociCedolino(db);
        AllineaVincoliTenantAnagrafiche(db);
        // Gli indici arrivano per ultimi: possono citare colonne aggiunte da migrazioni successive
        // alla CreateTable (es. AiProposte.SlotAttivo), che esistono solo dopo l'allineamento.
        CompletaIndici(db);

        RegistraStoricoAdozioneAtomico(db, migrazioni);

        return $"schema preesistente adottato alle {migrazioni.Count} migrazioni "
            + $"(fino a {migrazioni[^1]})"
            + (tabelleCreate.Count == 0
                ? "; nessuna tabella da completare"
                : $"; completate {tabelleCreate.Count} tabelle: {string.Join(", ", tabelleCreate)}")
            + $"; cedolini pre-workflow riclassificati: {cedoliniPreWorkflow}";
    }

    /// <summary>
    /// Registra prima dei DDL una sentinella di adozione. La riga e la tabella vengono create nella
    /// stessa transazione: uno spegnimento non può lasciare una sentinella priva della testa attesa.
    /// Se la sentinella esiste già, soltanto la medesima testa può essere ripresa automaticamente.
    /// </summary>
    private static void IniziaOVerificaAdozione(PagheDbContext db, string migrazioneTesta)
    {
        if (!TabellePresenti(db).Contains(TabellaAdozione))
        {
            using var transazione = db.Database.BeginTransaction();
            db.Database.ExecuteSqlRaw(
                $"""
                CREATE TABLE "{TabellaAdozione}" (
                    "Id" INTEGER NOT NULL PRIMARY KEY,
                    "MigrazioneTesta" TEXT NOT NULL
                );
                """);
            using var inserisci = CreaComando(db,
                $"INSERT INTO \"{TabellaAdozione}\" (\"Id\", \"MigrazioneTesta\") "
                + "VALUES (1, @migrazioneTesta);");
            var parametro = inserisci.CreateParameter();
            parametro.ParameterName = "@migrazioneTesta";
            parametro.Value = migrazioneTesta;
            inserisci.Parameters.Add(parametro);
            inserisci.ExecuteNonQuery();
            transazione.Commit();
        }

        using var verifica = CreaComando(
            db,
            $"SELECT \"MigrazioneTesta\" FROM \"{TabellaAdozione}\" WHERE \"Id\" = 1;");
        var testaRegistrata = verifica.ExecuteScalar() as string;
        if (!string.Equals(testaRegistrata, migrazioneTesta, StringComparison.Ordinal))
            throw new InvalidOperationException(
                "Adozione interrotta: la sentinella dello schema appartiene a una diversa testa "
                + "delle migrazioni. Lo storico non è stato modificato; completare o verificare "
                + "manualmente l'adozione precedente prima di aggiornare.");
    }

    /// <summary>
    /// Crea lo storico, inserisce tutti i marker mancanti e rimuove la sentinella in una singola
    /// transazione. Una caduta durante il loop lascia quindi o il vecchio prefisso con sentinella,
    /// o l'intera catena senza sentinella: mai un nuovo stato ambiguo.
    /// </summary>
    private static void RegistraStoricoAdozioneAtomico(
        PagheDbContext db,
        IReadOnlyList<string> migrazioni)
    {
        // Su Npgsql IHistoryRepository può considerare configurato lo storico anche quando la
        // relazione non esiste ancora; interrogare GetAppliedMigrations in quel punto produce
        // quindi un SELECT verso una tabella assente. Il catalogo reale decide se leggere il
        // prefisso oppure partire dall'elenco vuoto della prima adozione.
        var applicate = TabellePresenti(db).Contains(HistoryRepository.DefaultTableName)
            ? db.Database.GetAppliedMigrations().ToList()
            : [];
        if (applicate.Count > migrazioni.Count
            || !applicate.SequenceEqual(migrazioni.Take(applicate.Count), StringComparer.Ordinal))
            throw new InvalidOperationException(
                "Adozione interrotta: lo storico parziale non è un prefisso esatto delle "
                + "migrazioni configurate. Nessun marker è stato aggiunto.");

        var storico = db.Database.GetService<IHistoryRepository>();
        using var transazione = db.Database.BeginTransaction();
        db.Database.ExecuteSqlRaw(storico.GetCreateIfNotExistsScript());
        foreach (var migrazione in migrazioni.Skip(applicate.Count))
            db.Database.ExecuteSqlRaw(
                storico.GetInsertScript(new HistoryRow(migrazione, ProductInfo.GetVersion())));
        db.Database.ExecuteSqlRaw($"DROP TABLE \"{TabellaAdozione}\";");
        transazione.Commit();
    }

    /// <summary>
    /// Converge gli schemi adottati senza storico verso le FK tenant-bound di rapporti, DURC e
    /// promemoria. Le migrazioni normali eseguono lo stesso preflight; qui è indispensabile
    /// ripeterlo perché l'adozione registra l'intera catena come applicata.
    /// </summary>
    private static void AllineaVincoliTenantAnagrafiche(PagheDbContext db)
    {
        var provider = db.Database.ProviderName;
        var sqlite = provider == "Microsoft.EntityFrameworkCore.Sqlite";
        var postgres = provider == "Npgsql.EntityFrameworkCore.PostgreSQL";
        if (!sqlite && !postgres)
            throw new NotSupportedException(
                $"Provider database non supportato per i vincoli tenant anagrafici: "
                + $"{provider ?? "(non configurato)"}.");

        var tabelle = TabellePresenti(db);
        SpecificaVincoloTenant[] vincoli =
        [
            new("Rapporti", "Dipendenti", "DipendenteId", "Id",
                "FK_Rapporti_Dipendenti_TenantId_DipendenteId",
                ReferentialAction.Cascade,
                "IX_Rapporti_TenantId_DipendenteId", ["TenantId", "DipendenteId"]),
            new("Durc", "Aziende", "AziendaId", "Id",
                "FK_Durc_Aziende_TenantId_AziendaId",
                ReferentialAction.Cascade,
                "IX_Durc_TenantId_AziendaId_DataScadenza",
                ["TenantId", "AziendaId", "DataScadenza"]),
            new("Promemoria", "Aziende", "AziendaId", "Id",
                "FK_Promemoria_Aziende_TenantId_AziendaId",
                ReferentialAction.Restrict,
                "IX_Promemoria_TenantId_AziendaId", ["TenantId", "AziendaId"]),
        ];
        var applicabili = vincoli
            .Where(v => tabelle.Contains(v.TabellaFiglia) && tabelle.Contains(v.TabellaPadre))
            .ToArray();
        if (applicabili.Length == 0) return;

        // Tutti i dati si verificano prima del primo DDL: un riferimento orfano o discordante non
        // viene mai "riparato" scegliendo silenziosamente un tenant e non produce falso storico.
        foreach (var vincolo in applicabili) VerificaDatiVincoloTenant(db, vincolo);
        if (tabelle.Contains("Aziende")) VerificaDuplicatiChiaveTenant(db, "Aziende");
        if (tabelle.Contains("Dipendenti")) VerificaDuplicatiChiaveTenant(db, "Dipendenti");
        if (sqlite)
            foreach (var vincolo in applicabili)
                VerificaNessunaColonnaExtraSqlite(db, vincolo.TabellaFiglia);

        using IDbContextTransaction? transazione = postgres ? db.Database.BeginTransaction() : null;
        try
        {
            if (tabelle.Contains("Aziende")) GarantisceChiaveTenantId(db, "Aziende");
            if (tabelle.Contains("Dipendenti")) GarantisceChiaveTenantId(db, "Dipendenti");
            foreach (var vincolo in applicabili)
            {
                if (!HaVincoloTenantComposito(db, vincolo))
                {
                    if (sqlite) InstallaVincoloTenantSqlite(db, vincolo);
                    else InstallaVincoloTenantPostgres(db, vincolo);
                }
                GarantisceIndiceTenant(db, vincolo);
                if (!HaVincoloTenantComposito(db, vincolo))
                    throw new InvalidOperationException(
                        $"Adozione interrotta: il vincolo tenant {vincolo.TabellaFiglia}→"
                        + $"{vincolo.TabellaPadre} non risulta installato. Lo storico non è stato completato.");
            }
            transazione?.Commit();
        }
        catch
        {
            if (sqlite)
            {
                try { EseguiNonQuery(db, "ROLLBACK;"); } catch { /* Nessuna transazione aperta. */ }
            }
            throw;
        }
        finally
        {
            if (sqlite) EseguiNonQuery(db, "PRAGMA foreign_keys = 1;");
        }
    }

    private sealed record SpecificaVincoloTenant(
        string TabellaFiglia,
        string TabellaPadre,
        string ColonnaFiglia,
        string ColonnaPadre,
        string NomeVincolo,
        ReferentialAction OnDelete,
        string NomeIndice,
        string[] ColonneIndice);

    private static void VerificaDatiVincoloTenant(
        PagheDbContext db, SpecificaVincoloTenant vincolo)
    {
        var opzionale = vincolo.TabellaFiglia == "Promemoria"
            ? $"f.\"{vincolo.ColonnaFiglia}\" IS NOT NULL AND "
            : "";
        var discordanti = EseguiScalareInt64(
            db,
            $"""
            SELECT COUNT(*)
            FROM "{vincolo.TabellaFiglia}" AS f
            LEFT JOIN "{vincolo.TabellaPadre}" AS p
              ON p."{vincolo.ColonnaPadre}" = f."{vincolo.ColonnaFiglia}"
            WHERE {opzionale}(p."{vincolo.ColonnaPadre}" IS NULL
                OR p."TenantId" <> f."TenantId");
            """);
        if (discordanti > 0)
            throw new InvalidOperationException(
                $"Adozione interrotta: {vincolo.TabellaFiglia} contiene riferimenti orfani o con "
                + "TenantId discordante. Nessun dato è stato modificato e lo storico non è stato completato.");
    }

    private static void VerificaDuplicatiChiaveTenant(PagheDbContext db, string tabella)
    {
        if (HaChiaveUnicaTenantId(db, tabella)) return;
        var duplicati = EseguiScalareInt64(
            db,
            $"""
            SELECT COUNT(*)
            FROM (
                SELECT "TenantId", "Id"
                FROM "{tabella}"
                GROUP BY "TenantId", "Id"
                HAVING COUNT(*) > 1
            ) AS duplicati;
            """);
        if (duplicati > 0)
            throw new InvalidOperationException(
                $"Adozione interrotta: la chiave (TenantId, Id) di {tabella} contiene duplicati. "
                + "Lo storico non è stato completato.");
    }

    private static void GarantisceChiaveTenantId(PagheDbContext db, string tabella)
    {
        if (HaChiaveUnicaTenantId(db, tabella)) return;
        var nome = $"AK_{tabella}_TenantId_Id";
        if (IndiceConNomeEsiste(db, tabella, nome))
            throw new InvalidOperationException(
                $"Adozione interrotta: l'indice {nome} esiste ma non è una chiave unica "
                + "(TenantId, Id). Lo storico non è stato completato.");
        EseguiNonQuery(
            db,
            $"CREATE UNIQUE INDEX \"{nome}\" ON \"{tabella}\" (\"TenantId\", \"Id\");");
    }

    private static void VerificaNessunaColonnaExtraSqlite(PagheDbContext db, string tabella)
    {
        var colonneDatabase = Nomi(
            db,
            $"SELECT name FROM pragma_table_info('{tabella}');",
            "SELECT NULL WHERE 0;");
        var colonneModello = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entita in db.Model.GetEntityTypes()
                     .Where(e => string.Equals(
                         e.GetTableName(), tabella, StringComparison.OrdinalIgnoreCase)))
        {
            var oggetto = StoreObjectIdentifier.Table(tabella, entita.GetSchema());
            foreach (var proprieta in entita.GetProperties())
            {
                var colonna = proprieta.GetColumnName(oggetto);
                if (colonna is not null) colonneModello.Add(colonna);
            }
        }

        var extra = colonneDatabase.Except(colonneModello, StringComparer.OrdinalIgnoreCase)
            .OrderBy(c => c, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (extra.Length > 0)
            throw new InvalidOperationException(
                $"Adozione interrotta: {tabella} contiene colonne extra non rappresentate dal "
                + $"modello ({string.Join(", ", extra)}). La ricostruzione SQLite non è stata "
                + "eseguita, per evitare perdita silenziosa di dati; lo storico non è stato completato.");
    }

    private static void GarantisceIndiceTenant(PagheDbContext db, SpecificaVincoloTenant vincolo)
    {
        if (IndiceConNomeEsiste(db, vincolo.TabellaFiglia, vincolo.NomeIndice))
        {
            if (!IndiceConNomeHaColonne(
                    db, vincolo.TabellaFiglia, vincolo.NomeIndice, vincolo.ColonneIndice))
                throw new InvalidOperationException(
                    $"Adozione interrotta: l'indice {vincolo.NomeIndice} esiste con colonne "
                    + "diverse da quelle tenant-bound attese. Lo storico non è stato completato.");
            return;
        }
        var colonne = string.Join(", ", vincolo.ColonneIndice.Select(c => $"\"{c}\""));
        EseguiNonQuery(
            db,
            $"CREATE INDEX \"{vincolo.NomeIndice}\" ON \"{vincolo.TabellaFiglia}\" ({colonne});");
        if (!IndiceConNomeHaColonne(
                db, vincolo.TabellaFiglia, vincolo.NomeIndice, vincolo.ColonneIndice))
            throw new InvalidOperationException(
                $"Adozione interrotta: l'indice {vincolo.NomeIndice} non risulta installato.");
    }

    private static bool IndiceConNomeEsiste(PagheDbContext db, string tabella, string nome)
    {
        var sql = db.Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite"
            ? $"SELECT COUNT(*) FROM pragma_index_list('{tabella}') WHERE name = '{nome}';"
            : $"SELECT COUNT(*) FROM pg_indexes WHERE schemaname = 'public' "
                + $"AND tablename = '{tabella}' AND indexname = '{nome}';";
        return EseguiScalareInt64(db, sql) > 0;
    }

    private static bool IndiceConNomeHaColonne(
        PagheDbContext db, string tabella, string nome, IReadOnlyList<string> colonne)
    {
        var elenco = string.Join(",", colonne);
        var sql = db.Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite"
            ? $"""
              SELECT COUNT(*)
              FROM (
                  SELECT group_concat(name, ',') AS colonne
                  FROM (
                      SELECT name
                      FROM pragma_index_info('{nome}')
                      ORDER BY seqno)
              ) AS indice
              WHERE colonne = '{elenco}';
              """
            : $"""
              SELECT COUNT(*)
              FROM pg_class AS indice
              JOIN pg_index AS definizione ON definizione.indexrelid = indice.oid
              WHERE indice.relname = '{nome}'
                AND definizione.indrelid = '"{tabella}"'::regclass
                AND (
                    SELECT string_agg(attributo.attname, ',' ORDER BY colonna.posizione)
                    FROM unnest(definizione.indkey::smallint[])
                        WITH ORDINALITY AS colonna(numero, posizione)
                    JOIN pg_attribute AS attributo
                      ON attributo.attrelid = definizione.indrelid
                     AND attributo.attnum = colonna.numero
                ) = '{elenco}';
              """;
        return EseguiScalareInt64(db, sql) > 0;
    }

    private static void InstallaVincoloTenantSqlite(
        PagheDbContext db, SpecificaVincoloTenant vincolo)
    {
        var operazione = new AddForeignKeyOperation
        {
            Name = vincolo.NomeVincolo,
            Table = vincolo.TabellaFiglia,
            Columns = ["TenantId", vincolo.ColonnaFiglia],
            PrincipalTable = vincolo.TabellaPadre,
            PrincipalColumns = ["TenantId", vincolo.ColonnaPadre],
            OnDelete = vincolo.OnDelete,
        };
        var modelloDesignTime = db.GetService<IDesignTimeModel>().Model;
        var comandi = db.Database.GetService<IMigrationsSqlGenerator>()
            .Generate([operazione], modelloDesignTime);
        foreach (var comando in comandi)
            db.Database.ExecuteSqlRaw(comando.CommandText);
    }

    private static void InstallaVincoloTenantPostgres(
        PagheDbContext db, SpecificaVincoloTenant vincolo)
    {
        var eliminazione = vincolo.OnDelete == ReferentialAction.Cascade ? "CASCADE" : "RESTRICT";
        EseguiNonQuery(
            db,
            $"""
            DO $paghe$
            DECLARE vincolo_esistente record;
            BEGIN
                FOR vincolo_esistente IN
                    SELECT c.conname
                    FROM pg_constraint AS c
                    WHERE c.contype = 'f'
                      AND c.conrelid = '"{vincolo.TabellaFiglia}"'::regclass
                      AND c.confrelid = '"{vincolo.TabellaPadre}"'::regclass
                LOOP
                    EXECUTE format(
                        'ALTER TABLE "{vincolo.TabellaFiglia}" DROP CONSTRAINT %I',
                        vincolo_esistente.conname);
                END LOOP;
            END
            $paghe$;

            ALTER TABLE "{vincolo.TabellaFiglia}"
                ADD CONSTRAINT "{vincolo.NomeVincolo}"
                FOREIGN KEY ("TenantId", "{vincolo.ColonnaFiglia}")
                REFERENCES "{vincolo.TabellaPadre}" ("TenantId", "{vincolo.ColonnaPadre}")
                ON DELETE {eliminazione};
            """);
    }

    private static bool HaChiaveUnicaTenantId(PagheDbContext db, string tabella)
    {
        if (tabella is not ("Aziende" or "Dipendenti"))
            throw new ArgumentOutOfRangeException(nameof(tabella));
        var sql = db.Database.ProviderName switch
        {
            "Microsoft.EntityFrameworkCore.Sqlite" =>
                $"""
                SELECT COUNT(*)
                FROM (
                    SELECT il.name
                    FROM pragma_index_list('{tabella}') AS il
                    JOIN pragma_index_info(il.name) AS ii
                    WHERE il."unique" = 1 AND il."partial" = 0
                    GROUP BY il.name
                    HAVING COUNT(*) = 2
                       AND SUM(CASE WHEN ii.name = 'TenantId' THEN 1 ELSE 0 END) = 1
                       AND SUM(CASE WHEN ii.name = 'Id' THEN 1 ELSE 0 END) = 1
                ) AS chiavi;
                """,
            "Npgsql.EntityFrameworkCore.PostgreSQL" =>
                $"""
                SELECT COUNT(*)
                FROM pg_index AS i
                WHERE i.indrelid = '"{tabella}"'::regclass
                  AND i.indisunique
                  AND i.indpred IS NULL
                  AND i.indisvalid
                  AND i.indisready
                  AND (
                      SELECT COUNT(*) = 2
                         AND COUNT(*) FILTER (WHERE a.attname = 'TenantId') = 1
                         AND COUNT(*) FILTER (WHERE a.attname = 'Id') = 1
                      FROM unnest(i.indkey::smallint[]) AS numero_colonna
                      JOIN pg_attribute AS a
                        ON a.attrelid = i.indrelid AND a.attnum = numero_colonna
                  );
                """,
            _ => throw new NotSupportedException(
                $"Provider database non supportato per la chiave tenant: "
                + $"{db.Database.ProviderName ?? "(non configurato)"}."),
        };
        return EseguiScalareInt64(db, sql) > 0;
    }

    private static bool HaVincoloTenantComposito(
        PagheDbContext db, SpecificaVincoloTenant vincolo)
    {
        var filtroAzioneSqlite = vincolo.OnDelete == ReferentialAction.Cascade
            ? "UPPER(\"on_delete\") = 'CASCADE'"
            : "UPPER(\"on_delete\") IN ('RESTRICT', 'NO ACTION')";
        var filtroAzionePostgres = vincolo.OnDelete == ReferentialAction.Cascade
            ? "c.confdeltype = 'c'"
            : "c.confdeltype IN ('r', 'a')";
        var sql = db.Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite"
            ? $"""
              SELECT COUNT(*)
              FROM (
                  SELECT id
                  FROM pragma_foreign_key_list('{vincolo.TabellaFiglia}')
                  WHERE "table" = '{vincolo.TabellaPadre}'
                    AND {filtroAzioneSqlite}
                  GROUP BY id
                  HAVING COUNT(*) = 2
                     AND SUM(CASE WHEN "from" = 'TenantId' AND "to" = 'TenantId' THEN 1 ELSE 0 END) = 1
                     AND SUM(CASE WHEN "from" = '{vincolo.ColonnaFiglia}' AND "to" = '{vincolo.ColonnaPadre}' THEN 1 ELSE 0 END) = 1
              ) AS fk;
              """
            : $"""
              SELECT COUNT(*)
              FROM pg_constraint AS c
              WHERE c.contype = 'f'
                AND c.conrelid = '"{vincolo.TabellaFiglia}"'::regclass
                AND c.confrelid = '"{vincolo.TabellaPadre}"'::regclass
                AND {filtroAzionePostgres}
                AND (
                    SELECT COUNT(*) = 2
                       AND COUNT(*) FILTER (
                           WHERE figlia.attname = 'TenantId' AND padre.attname = 'TenantId') = 1
                       AND COUNT(*) FILTER (
                           WHERE figlia.attname = '{vincolo.ColonnaFiglia}' AND padre.attname = '{vincolo.ColonnaPadre}') = 1
                    FROM unnest(c.conkey, c.confkey) AS coppia(figlia_num, padre_num)
                    JOIN pg_attribute AS figlia
                      ON figlia.attrelid = c.conrelid AND figlia.attnum = coppia.figlia_num
                    JOIN pg_attribute AS padre
                      ON padre.attrelid = c.confrelid AND padre.attnum = coppia.padre_num
                );
              """;
        return EseguiScalareInt64(db, sql) > 0;
    }

    /// <summary>
    /// Riconcilia l'unico schema ibrido storicamente prodotto da <c>AllineaColonneMancanti</c>:
    /// storico fermo a WorkflowCedolinoImmutabile ma quattro colonne Residui/ANF già presenti.
    ///
    /// Non è una scorciatoia generale sulle migrazioni. Se le colonne sono parziali, nullable o di
    /// tipo diverso, l'avvio si interrompe senza toccare lo storico. Se sono equivalenti al modello,
    /// normalizza il valore neutro legacy dello stato ANF e registra soltanto quella migrazione.
    /// Classificazione PreWorkflow e FK tenant-bound restano pending e vengono eseguite da EF.
    /// </summary>
    private static void RiconciliaResiduiAnfGiaPresenti(
        PagheDbContext db,
        IReadOnlyList<string> migrazioniPending)
    {
        const string nome = "ResiduiRapportoEAssegniFamiliari";
        if (migrazioniPending.Count == 0
            || !migrazioniPending[0].EndsWith($"_{nome}", StringComparison.Ordinal))
            return;

        var presenti = ContaColonneResiduiAnf(db, richiediCompatibilita: false);
        if (presenti == 0) return; // Schema normale: sarà EF ad applicare la migrazione.
        if (presenti != 4)
            throw new InvalidOperationException(
                "Schema ibrido Residui/ANF incompleto: sono presenti solo alcune delle quattro "
                + "colonne attese. La migrazione non è stata registrata; correggere lo schema "
                + "prima di riavviare.");

        var compatibili = ContaColonneResiduiAnf(db, richiediCompatibilita: true);
        if (compatibili != 4)
            throw new InvalidOperationException(
                "Schema ibrido Residui/ANF non equivalente: tipo o nullability delle colonne non "
                + "corrisponde alla migrazione ufficiale. Lo storico non è stato modificato.");

        var statiNonAmmessi = EseguiScalareInt64(
            db,
            """
            SELECT COUNT(*)
            FROM "Cartellini"
            WHERE TRIM(COALESCE("StatoAssegniFamiliari", '')) NOT IN
                ('', 'DaVerificare', 'NonSpettanti', 'Spettanti');
            """);
        if (statiNonAmmessi > 0)
            throw new InvalidOperationException(
                "Schema ibrido Residui/ANF contiene stati ANF sconosciuti. La migrazione non è "
                + "stata registrata e nessun dato è stato normalizzato.");

        using var transazione = db.Database.BeginTransaction();

        // Conserviamo i valori già compilati sotto nomi temporanei, poi eseguiamo davvero le
        // AddColumn della migrazione ufficiale. In questo modo anche tipo, NOT NULL e DEFAULT del
        // database coincidono con il DDL versionato; non ci limitiamo a marchiarlo equivalente.
        db.Database.ExecuteSqlRaw(
            """
            ALTER TABLE "Rapporti" RENAME COLUMN "FerieResidueIniziali"
                TO "__AdozioneFerieResidueIniziali";
            ALTER TABLE "Rapporti" RENAME COLUMN "PermessiResiduiIniziali"
                TO "__AdozionePermessiResiduiIniziali";
            ALTER TABLE "Cartellini" RENAME COLUMN "ImportoAssegniFamiliari"
                TO "__AdozioneImportoAssegniFamiliari";
            ALTER TABLE "Cartellini" RENAME COLUMN "StatoAssegniFamiliari"
                TO "__AdozioneStatoAssegniFamiliari";
            """);

        var assembly = db.Database.GetService<IMigrationsAssembly>();
        var tipoMigrazione = assembly.Migrations.SingleOrDefault(
            m => m.Key.Equals(migrazioniPending[0], StringComparison.Ordinal));
        if (tipoMigrazione.Value is null)
            throw new InvalidOperationException(
                $"Migrazione {migrazioniPending[0]} non trovata nell'assembly configurato.");
        var migrazione = assembly.CreateMigration(tipoMigrazione.Value, db.Database.ProviderName!);
        var modelloDesignTime = db.GetService<IDesignTimeModel>().Model;
        var comandi = db.Database.GetService<IMigrationsSqlGenerator>()
            .Generate(migrazione.UpOperations, modelloDesignTime);
        foreach (var comando in comandi)
            db.Database.ExecuteSqlRaw(comando.CommandText);

        db.Database.ExecuteSqlRaw(
            """
            UPDATE "Rapporti"
            SET "FerieResidueIniziali" = "__AdozioneFerieResidueIniziali",
                "PermessiResiduiIniziali" = "__AdozionePermessiResiduiIniziali";
            UPDATE "Cartellini"
            SET "ImportoAssegniFamiliari" = "__AdozioneImportoAssegniFamiliari",
                "StatoAssegniFamiliari" = CASE
                    WHEN TRIM(COALESCE("__AdozioneStatoAssegniFamiliari", '')) = ''
                        THEN 'DaVerificare'
                    ELSE "__AdozioneStatoAssegniFamiliari"
                END;
            ALTER TABLE "Rapporti" DROP COLUMN "__AdozioneFerieResidueIniziali";
            ALTER TABLE "Rapporti" DROP COLUMN "__AdozionePermessiResiduiIniziali";
            ALTER TABLE "Cartellini" DROP COLUMN "__AdozioneImportoAssegniFamiliari";
            ALTER TABLE "Cartellini" DROP COLUMN "__AdozioneStatoAssegniFamiliari";
            """);

        var storico = db.Database.GetService<IHistoryRepository>();
        db.Database.ExecuteSqlRaw(
            storico.GetInsertScript(
                new HistoryRow(migrazioniPending[0], ProductInfo.GetVersion())));
        transazione.Commit();
    }

    private static long ContaColonneResiduiAnf(PagheDbContext db, bool richiediCompatibilita)
    {
        var provider = db.Database.ProviderName;
        if (provider == "Microsoft.EntityFrameworkCore.Sqlite")
        {
            var condizione = richiediCompatibilita
                ? " AND UPPER(type) = 'TEXT' AND \"notnull\" = 1"
                : "";
            return EseguiScalareInt64(
                db,
                $"""
                SELECT
                    (SELECT COUNT(*) FROM pragma_table_info('Rapporti')
                     WHERE name IN ('FerieResidueIniziali', 'PermessiResiduiIniziali'){condizione})
                  + (SELECT COUNT(*) FROM pragma_table_info('Cartellini')
                     WHERE name IN ('ImportoAssegniFamiliari', 'StatoAssegniFamiliari'){condizione});
                """);
        }

        if (provider == "Npgsql.EntityFrameworkCore.PostgreSQL")
        {
            var condizioneNumeri = richiediCompatibilita
                ? " AND data_type = 'numeric' AND numeric_precision = 15 "
                    + "AND numeric_scale = 2 AND is_nullable = 'NO'"
                : "";
            var condizioneStato = richiediCompatibilita
                ? " AND data_type = 'text' AND is_nullable = 'NO'"
                : "";
            return EseguiScalareInt64(
                db,
                $"""
                SELECT
                    (SELECT COUNT(*) FROM information_schema.columns
                     WHERE table_schema = 'public' AND table_name = 'Rapporti'
                       AND column_name IN ('FerieResidueIniziali', 'PermessiResiduiIniziali')
                       {condizioneNumeri})
                  + (SELECT COUNT(*) FROM information_schema.columns
                     WHERE table_schema = 'public' AND table_name = 'Cartellini'
                       AND column_name = 'ImportoAssegniFamiliari'{condizioneNumeri})
                  + (SELECT COUNT(*) FROM information_schema.columns
                     WHERE table_schema = 'public' AND table_name = 'Cartellini'
                       AND column_name = 'StatoAssegniFamiliari'{condizioneStato});
                """);
        }

        throw new NotSupportedException(
            $"Provider database non supportato per la riconciliazione Residui/ANF: "
            + $"{provider ?? "(non configurato)"}.");
    }

    /// <summary>
    /// Installa anche durante l'adozione il vincolo tenant-bound tra cedolino e voci. Registrare
    /// direttamente tutta la catena delle migrazioni senza questo passaggio lascerebbe infatti
    /// attivo il vecchio FK sul solo CedolinoId (o nessun FK), pur dichiarando applicata la
    /// migrazione <c>VincoloTenantVociCedolino</c>.
    ///
    /// Il riallineamento automatico è volutamente ristretto alla sentinella legacy Guid.Empty con
    /// payload vuoto. Un payload AEAD è associato al TenantId usato come contesto crittografico:
    /// riscriverlo silenziosamente lo renderebbe illeggibile. Qualunque altra discordanza, inclusi
    /// i riferimenti orfani, interrompe l'adozione prima che venga creato lo storico EF.
    /// </summary>
    private static void AllineaVincoloTenantVociCedolino(PagheDbContext db)
    {
        var tabelle = TabellePresenti(db);
        if (!tabelle.Contains("CedolinoVoceEntity") || !tabelle.Contains("Cedolini")) return;
        if (HaVincoloTenantVociCedolino(db)) return;

        var colonneVoci = Nomi(
            db,
            "SELECT name FROM pragma_table_info('CedolinoVoceEntity');",
            "SELECT column_name FROM information_schema.columns "
                + "WHERE table_schema = 'public' AND table_name = 'CedolinoVoceEntity';");
        string[] richiesteVoci = ["Id", "TenantId", "CedolinoId", "DatiVoceProtetti", "Ordine"];
        if (richiesteVoci.Any(c => !colonneVoci.Contains(c)))
            throw new InvalidOperationException(
                "Impossibile adottare CedolinoVoceEntity: mancano colonne necessarie al vincolo tenant-bound.");

        const string guidVuoto = "00000000-0000-0000-0000-000000000000";
        var provider = db.Database.ProviderName;
        var sqlite = provider == "Microsoft.EntityFrameworkCore.Sqlite";
        var postgres = provider == "Npgsql.EntityFrameworkCore.PostgreSQL";
        if (!sqlite && !postgres)
            throw new NotSupportedException(
                $"Provider database non supportato per l'adozione tenant-bound: {provider ?? "(non configurato)"}.");
        // La migrazione SQLite ricostruisce CedolinoVoceEntity dalle colonne mappate nel modello.
        // Blocchiamo prima di qualsiasi riallineamento o DDL se la tabella contiene colonne
        // manuali: altrimenti il rebuild le eliminerebbe insieme ai relativi valori.
        if (sqlite) VerificaNessunaColonnaExtraSqlite(db, "CedolinoVoceEntity");
        using IDbContextTransaction? transazione = sqlite ? null : db.Database.BeginTransaction();
        var trim = sqlite ? "TRIM" : "BTRIM";

        // Prima si verifica tutto: in presenza di un solo payload cifrato discordante non viene
        // modificata neppure una sentinella sicura e lo storico non può risultare falsamente completo.
        var nonRiparabili = EseguiScalareInt64(
            db,
            $"""
            SELECT COUNT(*)
            FROM "CedolinoVoceEntity" AS v
            LEFT JOIN "Cedolini" AS c ON c."Id" = v."CedolinoId"
            WHERE c."Id" IS NULL
               OR (
                    v."TenantId" <> c."TenantId"
                    AND NOT (
                        v."TenantId" = '{guidVuoto}'
                        AND {trim}(COALESCE(v."DatiVoceProtetti", '')) = '')
                  );
            """);
        if (nonRiparabili > 0)
            throw new InvalidOperationException(
                "Adozione interrotta: esistono voci cedolino orfane o con TenantId discordante "
                + "non riallineabile in sicurezza (payload potenzialmente legato al tenant tramite AEAD). "
                + "Lo storico delle migrazioni non è stato completato.");

        GarantisceChiaveTenantCedolino(db);

        var sqlRiallineamento = sqlite
            ? $"""
            UPDATE "CedolinoVoceEntity"
            SET "TenantId" = (
                SELECT c."TenantId"
                FROM "Cedolini" AS c
                WHERE c."Id" = "CedolinoVoceEntity"."CedolinoId")
            WHERE "TenantId" = '{guidVuoto}'
              AND {trim}(COALESCE("DatiVoceProtetti", '')) = ''
              AND EXISTS (
                  SELECT 1
                  FROM "Cedolini" AS c
                  WHERE c."Id" = "CedolinoVoceEntity"."CedolinoId");
            """
            : $"""
            UPDATE "CedolinoVoceEntity" AS v
            SET "TenantId" = c."TenantId"
            FROM "Cedolini" AS c
            WHERE c."Id" = v."CedolinoId"
              AND v."TenantId" = '{guidVuoto}'
              AND BTRIM(COALESCE(v."DatiVoceProtetti", '')) = '';
            """;
        EseguiNonQuery(db, sqlRiallineamento);

        if (sqlite)
            InstallaVincoloTenantVociSqlite(db);
        else
            InstallaVincoloTenantVociPostgres(db);

        if (!HaVincoloTenantVociCedolino(db))
            throw new InvalidOperationException(
                "Adozione interrotta: il vincolo tenant-bound delle voci cedolino non risulta installato. "
                + "Lo storico delle migrazioni non è stato completato.");

        // PostgreSQL applica preflight, riallineamento e DDL in un'unica transazione. SQLite deve
        // invece usare la sequenza PRAGMA/ricostruzione generata dalla migrazione ufficiale.
        transazione?.Commit();
    }

    private static void GarantisceChiaveTenantCedolino(PagheDbContext db)
    {
        if (HaChiaveUnicaTenantCedolino(db)) return;

        var duplicati = EseguiScalareInt64(
            db,
            """
            SELECT COUNT(*)
            FROM (
                SELECT "TenantId", "Id"
                FROM "Cedolini"
                GROUP BY "TenantId", "Id"
                HAVING COUNT(*) > 1
            ) AS duplicati;
            """);
        if (duplicati > 0)
            throw new InvalidOperationException(
                "Adozione interrotta: la chiave (TenantId, Id) dei cedolini contiene duplicati. "
                + "Il vincolo delle voci e lo storico delle migrazioni non sono stati modificati.");

        const string nome = "AK_Cedolini_TenantId_Id";
        if (IndiceConNomeEsiste(db, "Cedolini", nome))
            throw new InvalidOperationException(
                $"Adozione interrotta: l'indice {nome} esiste ma non è una chiave unica, valida "
                + "e non parziale (TenantId, Id). Lo storico non è stato completato.");

        // Anche gli schemi precedenti al workflow devono avere una chiave candidata valida prima
        // che SQLite/PostgreSQL accettino il nuovo FK composito. Solo un indice UNIQUE completo,
        // valido e non parziale può essere usato come chiave referenziata.
        EseguiNonQuery(
            db,
            $"CREATE UNIQUE INDEX \"{nome}\" ON \"Cedolini\" (\"TenantId\", \"Id\");");
        if (!HaChiaveUnicaTenantCedolino(db))
            throw new InvalidOperationException(
                "Adozione interrotta: la chiave tenant dei cedolini non risulta installata. "
                + "Lo storico non è stato completato.");
    }

    private static bool HaChiaveUnicaTenantCedolino(PagheDbContext db)
    {
        var provider = db.Database.ProviderName;
        var sql = provider switch
        {
            "Microsoft.EntityFrameworkCore.Sqlite" =>
                """
                SELECT COUNT(*)
                FROM (
                    SELECT il.name
                    FROM pragma_index_list('Cedolini') AS il
                    JOIN pragma_index_info(il.name) AS ii
                    WHERE il."unique" = 1 AND il."partial" = 0
                    GROUP BY il.name
                    HAVING COUNT(*) = 2
                       AND SUM(CASE WHEN ii.name = 'TenantId' THEN 1 ELSE 0 END) = 1
                       AND SUM(CASE WHEN ii.name = 'Id' THEN 1 ELSE 0 END) = 1
                ) AS chiavi;
                """,
            "Npgsql.EntityFrameworkCore.PostgreSQL" =>
                """
                SELECT COUNT(*)
                FROM pg_index AS i
                WHERE i.indrelid = '"Cedolini"'::regclass
                  AND i.indisunique
                  AND i.indpred IS NULL
                  AND i.indisvalid
                  AND i.indisready
                  AND (
                      SELECT COUNT(*) = 2
                         AND COUNT(*) FILTER (WHERE a.attname = 'TenantId') = 1
                         AND COUNT(*) FILTER (WHERE a.attname = 'Id') = 1
                      FROM unnest(i.indkey::smallint[]) AS numero_colonna
                      JOIN pg_attribute AS a
                        ON a.attrelid = i.indrelid AND a.attnum = numero_colonna
                  );
                """,
            _ => throw new NotSupportedException(
                $"Provider database non supportato per la chiave tenant cedolino: "
                + $"{provider ?? "(non configurato)"}."),
        };
        return EseguiScalareInt64(db, sql) > 0;
    }

    private static void InstallaVincoloTenantVociSqlite(PagheDbContext db)
    {
        // La migrazione ufficiale contiene la ricostruzione SQLite della tabella. Il vecchio indice
        // viene assicurato qui perché uno schema senza FK può non averlo: la ricostruzione resta la
        // stessa, versionata e verificata dal modello EF, senza DDL duplicato nel bootstrap.
        db.Database.ExecuteSqlRaw(
            "CREATE INDEX IF NOT EXISTS \"IX_CedolinoVoceEntity_CedolinoId\" "
            + "ON \"CedolinoVoceEntity\" (\"CedolinoId\");");

        var assembly = db.Database.GetService<IMigrationsAssembly>();
        var voceMigrazione = assembly.Migrations.SingleOrDefault(
            m => m.Key.EndsWith("_VincoloTenantVociCedolino", StringComparison.Ordinal));
        if (voceMigrazione.Value is null)
            throw new InvalidOperationException(
                "Migrazione VincoloTenantVociCedolino non trovata nell'assembly SQLite.");

        var migrazione = assembly.CreateMigration(voceMigrazione.Value, db.Database.ProviderName!);
        var modelloDesignTime = db.GetService<IDesignTimeModel>().Model;
        var comandi = db.Database.GetService<IMigrationsSqlGenerator>()
            .Generate(migrazione.UpOperations, modelloDesignTime);
        try
        {
            foreach (var comando in comandi)
                db.Database.ExecuteSqlRaw(comando.CommandText);
        }
        catch
        {
            // La ricostruzione SQLite sospende i FK tra due comandi distinti. Se un DDL fallisce,
            // chiudiamo l'eventuale transazione rimasta aperta e riattiviamo sempre la protezione:
            // il processo non deve proseguire con foreign_keys disabilitato.
            try { EseguiNonQuery(db, "ROLLBACK;"); } catch { /* Nessuna transazione aperta. */ }
            throw;
        }
        finally
        {
            EseguiNonQuery(db, "PRAGMA foreign_keys = 1;");
        }
    }

    private static void InstallaVincoloTenantVociPostgres(PagheDbContext db)
    {
        // Rimuove soltanto FK della tabella voci che puntano a Cedolini; eventuali nomi legacy non
        // sono affidabili. Il nuovo vincolo è aggiunto dopo il preflight dei dati ed è quindi
        // fail-closed anche se lo schema non aveva alcun FK.
        db.Database.ExecuteSqlRaw(
            """
            DO $paghe$
            DECLARE vincolo record;
            BEGIN
                FOR vincolo IN
                    SELECT c.conname
                    FROM pg_constraint AS c
                    WHERE c.contype = 'f'
                      AND c.conrelid = '"CedolinoVoceEntity"'::regclass
                      AND c.confrelid = '"Cedolini"'::regclass
                LOOP
                    EXECUTE format(
                        'ALTER TABLE "CedolinoVoceEntity" DROP CONSTRAINT %I',
                        vincolo.conname);
                END LOOP;
            END
            $paghe$;

            DROP INDEX IF EXISTS "IX_CedolinoVoceEntity_CedolinoId";
            CREATE INDEX IF NOT EXISTS "IX_CedolinoVoceEntity_TenantId_CedolinoId"
                ON "CedolinoVoceEntity" ("TenantId", "CedolinoId");
            ALTER TABLE "CedolinoVoceEntity"
                ADD CONSTRAINT "FK_CedolinoVoceEntity_Cedolini_TenantId_CedolinoId"
                FOREIGN KEY ("TenantId", "CedolinoId")
                REFERENCES "Cedolini" ("TenantId", "Id")
                ON DELETE CASCADE;
            """);
    }

    private static bool HaVincoloTenantVociCedolino(PagheDbContext db)
    {
        var sqlite = db.Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite";
        var sql = sqlite
            ? """
              SELECT COUNT(*)
              FROM (
                  SELECT id
                  FROM pragma_foreign_key_list('CedolinoVoceEntity')
                  WHERE "table" = 'Cedolini'
                  GROUP BY id
                  HAVING COUNT(*) = 2
                     AND SUM(CASE WHEN "from" = 'TenantId' AND "to" = 'TenantId' THEN 1 ELSE 0 END) = 1
                     AND SUM(CASE WHEN "from" = 'CedolinoId' AND "to" = 'Id' THEN 1 ELSE 0 END) = 1
              ) AS fk;
              """
            : """
              SELECT COUNT(*)
              FROM pg_constraint AS c
              WHERE c.contype = 'f'
                AND c.conrelid = '"CedolinoVoceEntity"'::regclass
                AND c.confrelid = '"Cedolini"'::regclass
                AND (
                    SELECT COUNT(*) = 2
                       AND COUNT(*) FILTER (
                           WHERE figlia.attname = 'TenantId' AND padre.attname = 'TenantId') = 1
                       AND COUNT(*) FILTER (
                           WHERE figlia.attname = 'CedolinoId' AND padre.attname = 'Id') = 1
                    FROM unnest(c.conkey, c.confkey) AS coppia(figlia_num, padre_num)
                    JOIN pg_attribute AS figlia
                      ON figlia.attrelid = c.conrelid AND figlia.attnum = coppia.figlia_num
                    JOIN pg_attribute AS padre
                      ON padre.attrelid = c.confrelid AND padre.attnum = coppia.padre_num
                );
              """;
        return EseguiScalareInt64(db, sql) > 0;
    }

    private static long EseguiScalareInt64(PagheDbContext db, string sql)
    {
        using var cmd = CreaComando(db, sql);
        return Convert.ToInt64(cmd.ExecuteScalar());
    }

    private static int EseguiNonQuery(PagheDbContext db, string sql)
    {
        using var cmd = CreaComando(db, sql);
        return cmd.ExecuteNonQuery();
    }

    private static System.Data.Common.DbCommand CreaComando(PagheDbContext db, string sql)
    {
        var conn = db.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open) conn.Open();
        var cmd = conn.CreateCommand();
        cmd.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
        cmd.CommandText = sql;
        return cmd;
    }

    /// <summary>
    /// Distingue i cedolini nati prima della macchina a stati dalle bozze create dal flusso
    /// corrente. Nell'adozione non possiamo basarci sul solo valore <c>Bozza</c>: una bozza nuova
    /// e valida ha già fotografia dei parametri e relativo hash. Le sentinelle congiunte evitano
    /// quindi di riclassificarla; l'assenza di snapshot e documento chiuso completa il controllo.
    ///
    /// Il metodo è intenzionalmente idempotente: dopo il primo passaggio lo stato non soddisfa più
    /// il predicato. Le tabelle di snapshot/documenti possono non esistere negli schemi più vecchi.
    /// </summary>
    private static int ClassificaCedoliniPreWorkflow(PagheDbContext db)
    {
        var tabelle = TabellePresenti(db);
        if (!tabelle.Contains("Cedolini")) return 0;

        var colonne = Nomi(
            db,
            "SELECT name FROM pragma_table_info('Cedolini');",
            "SELECT column_name FROM information_schema.columns "
                + "WHERE table_schema = 'public' AND table_name = 'Cedolini';");
        string[] richieste =
        [
            "Id", "TenantId", "Stato", "Versione", "HashParametriSha256",
            "ParametriApplicatiProtetti", "ControllatoIl", "ControllatoDa",
            "ValidatoIl", "ValidatoDa", "DatiValidazioneProtetti", "ChiusoIl", "ChiusoDa",
        ];
        if (richieste.Any(c => !colonne.Contains(c))) return 0;

        var senzaSnapshot = tabelle.Contains("CedoliniSnapshot")
            ? """
                  AND NOT EXISTS (
                      SELECT 1
                      FROM "CedoliniSnapshot" AS s
                      WHERE s."TenantId" = "Cedolini"."TenantId"
                        AND s."CedolinoId" = "Cedolini"."Id")
              """
            : "";
        var senzaDocumento = tabelle.Contains("CedoliniDocumenti")
            ? """
                  AND NOT EXISTS (
                      SELECT 1
                      FROM "CedoliniDocumenti" AS d
                      WHERE d."TenantId" = "Cedolini"."TenantId"
                        AND d."CedolinoId" = "Cedolini"."Id")
              """
            : "";

        // Stato vuoto/Versione 0 sono i default neutri aggiunti da AllineaColonneMancanti;
        // Bozza/Versione 1 sono invece i default della migrazione workflow originale.
        var sql =
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
            """
            + senzaSnapshot
            + senzaDocumento
            + ";";

        var conn = db.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open) conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        return cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Crea le tabelle e gli indici assenti riusando le operazioni di <b>tutte</b> le migrazioni,
    /// nell'ordine: il DDL resta generato da EF per il provider in uso, senza istruzioni scritte a
    /// mano. Le colonne le ha già allineate <c>AllineaColonneMancanti</c> sul modello corrente.
    ///
    /// Serve scorrere tutte le migrazioni, non solo la prima: dopo l'adozione lo storico dichiara
    /// applicata l'intera catena, quindi non c'è un <c>Migrate()</c> successivo che crei le tabelle
    /// introdotte dopo la migrazione iniziale.
    ///
    /// Non rimuove nulla: eventuali tabelle o indici lasciati da versioni precedenti del modello
    /// restano nel database (inutilizzati) invece di essere cancellati all'avvio.
    /// </summary>
    private static List<string> CompletaSchema(PagheDbContext db, List<string> tabelleMancanti)
    {
        var daCreare = new HashSet<string>(tabelleMancanti, StringComparer.OrdinalIgnoreCase);
        var operazioni = OperazioniMigrazioni(db, op =>
            op is CreateTableOperation t && daCreare.Contains(t.Name));
        EseguiOperazioni(db, operazioni);
        return operazioni.OfType<CreateTableOperation>().Select(t => t.Name).ToList();
    }

    /// <summary>
    /// Crea gli indici assenti: sia quelli delle tabelle appena completate sia quelli aggiunti al
    /// modello dopo la creazione di una tabella già esistente. Va chiamato dopo l'allineamento
    /// delle colonne, perché una migrazione può indicizzare colonne introdotte dopo la CreateTable.
    /// </summary>
    private static void CompletaIndici(PagheDbContext db)
    {
        var indiciPresenti = IndiciPresenti(db);
        EseguiOperazioni(db, OperazioniMigrazioni(db, op =>
            op is CreateIndexOperation i && !indiciPresenti.Contains(i.Name)));
    }

    /// <summary>Le operazioni di tutte le migrazioni, nell'ordine, filtrate dal predicato.</summary>
    private static List<MigrationOperation> OperazioniMigrazioni(
        PagheDbContext db, Func<MigrationOperation, bool> filtro)
    {
        var assembly = db.Database.GetService<IMigrationsAssembly>();
        var operazioni = new List<MigrationOperation>();
        foreach (var (_, tipo) in assembly.Migrations)
        {
            var migrazione = assembly.CreateMigration(tipo, db.Database.ProviderName!);
            operazioni.AddRange(migrazione.UpOperations.Where(filtro));
        }
        return operazioni;
    }

    private static void EseguiOperazioni(PagheDbContext db, List<MigrationOperation> operazioni)
    {
        if (operazioni.Count == 0) return;
        var comandi = db.Database.GetService<IMigrationsSqlGenerator>().Generate(operazioni, db.Model);
        foreach (var comando in comandi)
            db.Database.ExecuteSqlRaw(comando.CommandText);
    }

    /// <summary>Nomi delle tabelle presenti nel database (SQLite e PostgreSQL).</summary>
    private static HashSet<string> TabellePresenti(PagheDbContext db) => Nomi(
        db,
        "SELECT name FROM sqlite_master WHERE type = 'table';",
        "SELECT table_name FROM information_schema.tables WHERE table_schema = 'public';");

    /// <summary>Nomi degli indici presenti nel database (SQLite e PostgreSQL).</summary>
    private static HashSet<string> IndiciPresenti(PagheDbContext db) => Nomi(
        db,
        "SELECT name FROM sqlite_master WHERE type = 'index' AND name IS NOT NULL;",
        "SELECT indexname FROM pg_indexes WHERE schemaname = 'public';");

    private static HashSet<string> Nomi(PagheDbContext db, string sqlSqlite, string sqlPostgres)
    {
        bool sqlite = db.Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite";
        var nomi = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var conn = db.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open) conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = sqlite ? sqlSqlite : sqlPostgres;
        using var reader = cmd.ExecuteReader();
        while (reader.Read()) nomi.Add(reader.GetString(0));

        return nomi;
    }
}
