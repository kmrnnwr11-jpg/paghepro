using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
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
/// viene poi registrata come già applicata. I dati non vengono toccati.</item>
/// </list>
/// </summary>
public static class SchemaDatabase
{
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
        if (presenti.Contains(HistoryRepository.DefaultTableName)
            && db.Database.GetAppliedMigrations().Any())
        {
            var daApplicare = db.Database.GetPendingMigrations().ToList();
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

        // Adozione di uno schema preesistente: lo si porta al modello CORRENTE (colonne, tabelle e
        // indici mancanti) e si registrano TUTTE le migrazioni come applicate.
        //
        // Registrare solo la iniziale non funziona: AllineaColonneMancanti confronta con il modello
        // corrente, quindi aggiunge già le colonne introdotte dalle migrazioni successive, e
        // Migrate() fallirebbe poi con "duplicate column". Lo schema è a livello della testa: va
        // dichiarato tale.
        db.AllineaColonneMancanti();
        var tabelleCreate = CompletaSchema(db, attese.Where(t => !presenti.Contains(t)).ToList());

        var migrazioni = db.Database.GetMigrations().ToList();
        if (migrazioni.Count == 0)
            throw new InvalidOperationException(
                "Nessuna migrazione trovata nell'assembly configurato: verificare MigrationsAssembly " +
                "in Program.cs e il riferimento al progetto di migrazione.");

        // Gli script provengono da EF (nome della tabella di storico e valori), non da input utente.
        var storico = db.Database.GetService<IHistoryRepository>();
        db.Database.ExecuteSqlRaw(storico.GetCreateIfNotExistsScript());
        foreach (var migrazione in migrazioni)
            db.Database.ExecuteSqlRaw(
                storico.GetInsertScript(new HistoryRow(migrazione, ProductInfo.GetVersion())));

        return $"schema preesistente adottato alle {migrazioni.Count} migrazioni "
            + $"(fino a {migrazioni[^1]})"
            + (tabelleCreate.Count == 0
                ? "; nessuna tabella da completare"
                : $"; completate {tabelleCreate.Count} tabelle: {string.Join(", ", tabelleCreate)}");
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
        var assembly = db.Database.GetService<IMigrationsAssembly>();
        if (assembly.Migrations.Count == 0) return [];

        var daCreare = new HashSet<string>(tabelleMancanti, StringComparer.OrdinalIgnoreCase);
        var indiciPresenti = IndiciPresenti(db);

        var operazioni = new List<MigrationOperation>();
        foreach (var (_, tipo) in assembly.Migrations)
        {
            var migrazione = assembly.CreateMigration(tipo, db.Database.ProviderName!);
            operazioni.AddRange(migrazione.UpOperations.Where(op => op switch
            {
                CreateTableOperation t => daCreare.Contains(t.Name),
                // Copre sia gli indici delle tabelle nuove sia quelli aggiunti al modello dopo la
                // creazione di una tabella già esistente.
                CreateIndexOperation i => !indiciPresenti.Contains(i.Name),
                _ => false,
            }));
        }
        if (operazioni.Count == 0) return [];

        var comandi = db.Database.GetService<IMigrationsSqlGenerator>().Generate(operazioni, db.Model);
        foreach (var comando in comandi)
            db.Database.ExecuteSqlRaw(comando.CommandText);

        return operazioni.OfType<CreateTableOperation>().Select(t => t.Name).ToList();
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
