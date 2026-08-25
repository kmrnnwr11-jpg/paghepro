using System.Globalization;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using System.Text.RegularExpressions;
using Paghe.Persistence;
using Paghe.Persistence.Entities;
using Xunit;

namespace Paghe.Tests;

/// <summary>
/// Verifica che le relazioni anagrafiche usino il tenant come parte della chiave e che gli
/// archivi legacy discordanti vengano rifiutati senza attribuire una migrazione mai riuscita.
/// </summary>
public sealed class VincoliTenantAnagraficheTests : IDisposable
{
    private const string MigrazionePrecedente = "20260817222655_VincoloTenantVociCedolino";
    private const string MigrazioneCorrente = "20260817232753_VincoliTenantAnagrafiche";
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public VincoliTenantAnagraficheTests()
    {
        _connection.Open();
        using var abilita = _connection.CreateCommand();
        abilita.CommandText = "PRAGMA foreign_keys = 1;";
        abilita.ExecuteNonQuery();
    }

    public void Dispose() => _connection.Dispose();

    private PagheDbContext NuovoContesto() => new(
        new DbContextOptionsBuilder<PagheDbContext>()
            .UseSqlite(_connection, sql => sql.MigrationsAssembly("Paghe.Migrazioni.Sqlite"))
            .Options);

    private static AziendaEntity Azienda(Guid tenantId, string nome) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        RagioneSociale = nome,
    };

    private static DipendenteEntity Dipendente(Guid tenantId, string nome) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        CodiceFiscale = $"TEST{Guid.NewGuid():N}"[..16].ToUpperInvariant(),
        Nome = nome,
        Cognome = "Verifica",
    };

    private static RapportoLavoroEntity Rapporto(
        Guid tenantId, Guid aziendaId, Guid dipendenteId) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        AziendaId = aziendaId,
        DipendenteId = dipendenteId,
        CodiceCcnl = "COMMERCIO",
        Livello = "IV",
        DataAssunzione = new DateOnly(2026, 1, 1),
    };

    private static DurcEntity Durc(Guid tenantId, Guid aziendaId) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        AziendaId = aziendaId,
        Numero = $"D-{Guid.NewGuid():N}",
        DataEmissione = new DateOnly(2026, 1, 1),
        DataScadenza = new DateOnly(2026, 5, 1),
    };

    private static PromemoriaEntity Promemoria(
        Guid tenantId, Guid? aziendaId, string titolo) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        AziendaId = aziendaId,
        Tipo = TipoPromemoria.Manuale,
        Titolo = titolo,
        Descrizione = titolo,
        DataScadenza = new DateOnly(2026, 5, 1),
        Stato = StatoPromemoria.Aperto,
        Messaggio = titolo,
    };

    private static async Task<(AziendaEntity AziendaA, AziendaEntity AziendaB,
        DipendenteEntity DipendenteA, DipendenteEntity DipendenteB)> CreaPrincipaliAsync(
        PagheDbContext db, Guid tenantA, Guid tenantB)
    {
        var aziendaA = Azienda(tenantA, "Azienda A");
        var aziendaB = Azienda(tenantB, "Azienda B");
        var dipendenteA = Dipendente(tenantA, "Persona A");
        var dipendenteB = Dipendente(tenantB, "Persona B");
        await InserisciAziendaLegacyAsync(db, aziendaA);
        await InserisciAziendaLegacyAsync(db, aziendaB);
        await InserisciDipendenteLegacyAsync(db, dipendenteA);
        await InserisciDipendenteLegacyAsync(db, dipendenteB);
        return (aziendaA, aziendaB, dipendenteA, dipendenteB);
    }

    [Fact]
    public async Task MigrazioneValida_InstallaLeTreFkCompositeENegaOgniCrossTenant()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        await using var db = NuovoContesto();
        var migrator = db.Database.GetService<IMigrator>();
        migrator.Migrate(MigrazionePrecedente);
        var dati = await CreaPrincipaliAsync(db, tenantA, tenantB);
        await InserisciRapportoLegacyAsync(db, Rapporto(tenantA, dati.AziendaA.Id, dati.DipendenteA.Id));
        db.Durc.Add(Durc(tenantA, dati.AziendaA.Id));
        db.Promemoria.Add(Promemoria(tenantA, dati.AziendaA.Id, "Valido"));
        await db.SaveChangesAsync();

        migrator.Migrate();

        Assert.Contains(MigrazioneCorrente, db.Database.GetAppliedMigrations());
        Assert.Single(await db.Rapporti.Where(r => r.TenantId == tenantA).ToListAsync());
        Assert.Single(await db.Durc.Where(d => d.TenantId == tenantA).ToListAsync());
        Assert.Single(await db.Promemoria.Where(p => p.TenantId == tenantA).ToListAsync());

        await AssertFkAsync(db, Rapporto(tenantA, dati.AziendaA.Id, dati.DipendenteB.Id));
        await AssertFkAsync(db, Durc(tenantA, dati.AziendaB.Id));
        await AssertFkAsync(db, Promemoria(tenantA, dati.AziendaB.Id, "Cross tenant"));
    }

    [Theory]
    [InlineData("Rapporto")]
    [InlineData("Durc")]
    [InlineData("Promemoria")]
    public async Task MigrazioneConDatoDiscordante_FalliscePrimaDelDdlESenzaRegistrarsi(
        string tipo)
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        await using var db = NuovoContesto();
        var migrator = db.Database.GetService<IMigrator>();
        migrator.Migrate(MigrazionePrecedente);
        var dati = await CreaPrincipaliAsync(db, tenantA, tenantB);

        if (tipo == "Rapporto")
            await InserisciRapportoLegacyAsync(db, Rapporto(tenantA, dati.AziendaA.Id, dati.DipendenteB.Id));
        else if (tipo == "Durc")
            db.Durc.Add(Durc(tenantA, dati.AziendaB.Id));
        else
            db.Promemoria.Add(Promemoria(tenantA, dati.AziendaB.Id, "Cross tenant"));
        await db.SaveChangesAsync(); // lo schema precedente proteggeva soltanto l'Id

        var errore = Assert.Throws<SqliteException>(() => migrator.Migrate());

        Assert.Equal(19, errore.SqliteErrorCode);
        Assert.DoesNotContain(MigrazioneCorrente, db.Database.GetAppliedMigrations());
    }

    [Fact]
    public async Task AdozioneSenzaStorico_ConRapportoDiscordante_FallisceSenzaFalsoStorico()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        await using var db = NuovoContesto();
        db.Database.GetService<IMigrator>().Migrate(MigrazionePrecedente);
        var dati = await CreaPrincipaliAsync(db, tenantA, tenantB);
        await InserisciRapportoLegacyAsync(db, Rapporto(tenantA, dati.AziendaA.Id, dati.DipendenteB.Id));
        await db.Database.ExecuteSqlRawAsync("DROP TABLE \"__EFMigrationsHistory\";");

        var errore = Assert.Throws<InvalidOperationException>(() => SchemaDatabase.Applica(db));

        Assert.Contains("TenantId discordante", errore.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(await TabellaEsisteAsync("__EFMigrationsHistory"));
    }

    [Theory]
    [InlineData("Rapporti")]
    [InlineData("Durc")]
    [InlineData("Promemoria")]
    public async Task MigrazioneConOrfano_FallisceSenzaMarkerEPreservaLaRiga(string tabella)
    {
        await using var db = NuovoContesto();
        var migrator = db.Database.GetService<IMigrator>();
        migrator.Migrate(MigrazionePrecedente);
        var id = await InserisciOrfanoAsync(db, tabella);

        var errore = Assert.Throws<SqliteException>(() => migrator.Migrate());

        Assert.Equal(19, errore.SqliteErrorCode);
        Assert.DoesNotContain(MigrazioneCorrente, db.Database.GetAppliedMigrations());
        Assert.True(RigaEsiste(tabella, id));
    }

    [Theory]
    [InlineData("Rapporti")]
    [InlineData("Durc")]
    [InlineData("Promemoria")]
    public async Task AdozioneSenzaStorico_ConOrfano_FallisceSenzaMarkerEPreservaLaRiga(
        string tabella)
    {
        await using var db = NuovoContesto();
        db.Database.GetService<IMigrator>().Migrate(MigrazionePrecedente);
        var id = await InserisciOrfanoAsync(db, tabella);
        await db.Database.ExecuteSqlRawAsync("DROP TABLE \"__EFMigrationsHistory\";");

        var errore = Assert.Throws<InvalidOperationException>(() => SchemaDatabase.Applica(db));

        Assert.Contains("orfani", errore.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(await TabellaEsisteAsync("__EFMigrationsHistory"));
        Assert.True(RigaEsiste(tabella, id));
    }

    [Fact]
    public async Task AdozioneParziale_TabellaPromemoriaMancante_ConvergeSenzaDuplicareIndici()
    {
        var tenant = Guid.NewGuid();
        await using var db = NuovoContesto();
        db.Database.GetService<IMigrator>().Migrate(MigrazionePrecedente);
        var azienda = Azienda(tenant, "Azienda parziale");
        var dipendente = Dipendente(tenant, "Persona parziale");
        await InserisciAziendaLegacyAsync(db, azienda);
        await InserisciDipendenteLegacyAsync(db, dipendente);
        db.Durc.Add(Durc(tenant, azienda.Id));
        await db.SaveChangesAsync();
        await InserisciRapportoLegacyAsync(db, Rapporto(tenant, azienda.Id, dipendente.Id));
        await db.Database.ExecuteSqlRawAsync("DROP TABLE \"Promemoria\";");
        await db.Database.ExecuteSqlRawAsync("DROP TABLE \"__EFMigrationsHistory\";");

        var esito = SchemaDatabase.Applica(db);

        Assert.Contains("Promemoria", esito);
        Assert.True(await TabellaEsisteAsync("Promemoria"));
        AssertVincoliTenantInstallati();
        Assert.Contains(MigrazioneCorrente, db.Database.GetAppliedMigrations());
        Assert.Single(await db.Rapporti.Where(r => r.TenantId == tenant).ToListAsync());
        Assert.Single(await db.Durc.Where(d => d.TenantId == tenant).ToListAsync());
    }

    [Fact]
    public async Task AdozioneSenzaFkONomiLegacy_InstallaDalCatalogoEPreservaIRecord()
    {
        var tenant = Guid.NewGuid();
        await using var db = NuovoContesto();
        db.Database.GetService<IMigrator>().Migrate(MigrazionePrecedente);
        var azienda = Azienda(tenant, "Azienda legacy");
        var dipendente = Dipendente(tenant, "Persona legacy");
        await InserisciAziendaLegacyAsync(db, azienda);
        await InserisciDipendenteLegacyAsync(db, dipendente);
        db.Durc.Add(Durc(tenant, azienda.Id));
        db.Promemoria.Add(Promemoria(tenant, azienda.Id, "Promemoria legacy"));
        await db.SaveChangesAsync();
        await InserisciRapportoLegacyAsync(db, Rapporto(tenant, azienda.Id, dipendente.Id));

        foreach (var tabella in new[] { "Rapporti", "Durc", "Promemoria" })
            RicostruisciTabella(tabella, RimuoviForeignKey);
        await db.Database.ExecuteSqlRawAsync("DROP TABLE \"__EFMigrationsHistory\";");

        SchemaDatabase.Applica(db);

        AssertVincoliTenantInstallati();
        Assert.Single(await db.Rapporti.Where(r => r.TenantId == tenant).ToListAsync());
        Assert.Single(await db.Durc.Where(d => d.TenantId == tenant).ToListAsync());
        Assert.Single(await db.Promemoria.Where(p => p.TenantId == tenant).ToListAsync());
        Assert.Contains(MigrazioneCorrente, db.Database.GetAppliedMigrations());
    }

    [Fact]
    public async Task AdozioneUniqueParzialeSulParent_NonLoAccettaComeChiaveReferenziata()
    {
        var tenant = Guid.NewGuid();
        await using var db = NuovoContesto();
        db.Database.GetService<IMigrator>().Migrate(MigrazionePrecedente);
        var azienda = Azienda(tenant, "Azienda indice parziale");
        var dipendente = Dipendente(tenant, "Persona indice parziale");
        await InserisciAziendaLegacyAsync(db, azienda);
        await InserisciDipendenteLegacyAsync(db, dipendente);
        await db.SaveChangesAsync();
        await InserisciRapportoLegacyAsync(db, Rapporto(tenant, azienda.Id, dipendente.Id));
        RicostruisciTabella("Rapporti", RimuoviForeignKey);
        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE UNIQUE INDEX "UX_Dipendenti_TenantId_Id_Parziale"
                ON "Dipendenti" ("TenantId", "Id")
                WHERE "Anonimizzato" = 0;
            DROP TABLE "__EFMigrationsHistory";
            """);

        SchemaDatabase.Applica(db);

        Assert.True(HaChiaveTenantNonParziale("Dipendenti"));
        AssertVincoliTenantInstallati();
        Assert.Single(await db.Rapporti.Where(r => r.TenantId == tenant).ToListAsync());
        Assert.Contains(MigrazioneCorrente, db.Database.GetAppliedMigrations());
    }

    [Fact]
    public async Task AdozioneRapportiConColonnaExtra_FalliscePrimaDelRebuildEPreservaIlDato()
    {
        var tenant = Guid.NewGuid();
        await using var db = NuovoContesto();
        db.Database.GetService<IMigrator>().Migrate(MigrazionePrecedente);
        var azienda = Azienda(tenant, "Azienda colonna legacy");
        var dipendente = Dipendente(tenant, "Persona colonna legacy");
        var rapporto = Rapporto(tenant, azienda.Id, dipendente.Id);
        await InserisciAziendaLegacyAsync(db, azienda);
        await InserisciDipendenteLegacyAsync(db, dipendente);
        await db.SaveChangesAsync();
        await InserisciRapportoLegacyAsync(db, rapporto);
        RicostruisciTabella("Rapporti", RimuoviForeignKey);
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            ALTER TABLE "Rapporti" ADD COLUMN "NotaLegacy" TEXT;
            UPDATE "Rapporti" SET "NotaLegacy" = {"valore-da-preservare"}
            WHERE "Id" = {rapporto.Id};
            DROP TABLE "__EFMigrationsHistory";
            """);

        var errore = Assert.Throws<InvalidOperationException>(() => SchemaDatabase.Applica(db));

        Assert.Contains("colonne extra", errore.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("NotaLegacy", errore.Message, StringComparison.Ordinal);
        Assert.False(await TabellaEsisteAsync("__EFMigrationsHistory"));
        Assert.True(await TabellaEsisteAsync("Rapporti"));
        await using var verifica = _connection.CreateCommand();
        verifica.CommandText =
            "SELECT \"NotaLegacy\" FROM \"Rapporti\" WHERE \"Id\" = $id;";
        verifica.Parameters.AddWithValue("$id", rapporto.Id.ToString().ToUpperInvariant());
        Assert.Equal("valore-da-preservare", await verifica.ExecuteScalarAsync());
    }

    [Theory]
    [InlineData("Rapporti", "RESTRICT")]
    [InlineData("Durc", "RESTRICT")]
    [InlineData("Promemoria", "CASCADE")]
    public async Task AdozioneFkCompositaConDeleteErrato_ConvergeAllAzioneAttesa(
        string tabella, string azioneErrata)
    {
        await using var db = NuovoContesto();
        db.Database.Migrate();
        RicostruisciTabella(
            tabella,
            ddl => Regex.Replace(
                ddl,
                @"ON DELETE (?:CASCADE|RESTRICT|NO ACTION|SET NULL)",
                $"ON DELETE {azioneErrata}",
                RegexOptions.IgnoreCase));
        await db.Database.ExecuteSqlRawAsync("DROP TABLE \"__EFMigrationsHistory\";");

        SchemaDatabase.Applica(db);

        AssertVincoliTenantInstallati();
        Assert.Contains(MigrazioneCorrente, db.Database.GetAppliedMigrations());
    }

    [Fact]
    public async Task EliminaAzienda_ScollegaSoloPromemoriaDelTenantECascataSoloIlSuoDurc()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        await using (var db = NuovoContesto())
        {
            db.Database.Migrate();
            var dati = await CreaPrincipaliAsync(db, tenantA, tenantB);
            db.Promemoria.AddRange(
                Promemoria(tenantA, dati.AziendaA.Id, "Promemoria A"),
                Promemoria(tenantB, dati.AziendaB.Id, "Promemoria B"));
            db.Durc.AddRange(
                Durc(tenantA, dati.AziendaA.Id),
                Durc(tenantB, dati.AziendaB.Id));
            await db.SaveChangesAsync();

            Assert.True(await new AziendeService(db).EliminaAsync(tenantA, dati.AziendaA.Id));
        }

        await using var verifica = NuovoContesto();
        Assert.Null((await verifica.Promemoria.SingleAsync(p => p.Titolo == "Promemoria A")).AziendaId);
        Assert.NotNull((await verifica.Promemoria.SingleAsync(p => p.Titolo == "Promemoria B")).AziendaId);
        Assert.Empty(await verifica.Durc.Where(d => d.TenantId == tenantA).ToListAsync());
        Assert.Single(await verifica.Durc.Where(d => d.TenantId == tenantB).ToListAsync());
        Assert.Single(await verifica.Aziende.Where(a => a.TenantId == tenantB).ToListAsync());
    }

    [Fact]
    public async Task EliminaAziendeBulk_ScollegaTuttiISoliPromemoriaDelTenant()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var aziendaA1 = Azienda(tenantA, "A1");
        var aziendaA2 = Azienda(tenantA, "A2");
        var aziendaB = Azienda(tenantB, "B");
        await using (var db = NuovoContesto())
        {
            db.Database.Migrate();
            db.Aziende.AddRange(aziendaA1, aziendaA2, aziendaB);
            db.Promemoria.AddRange(
                Promemoria(tenantA, aziendaA1.Id, "P-A1"),
                Promemoria(tenantA, aziendaA2.Id, "P-A2"),
                Promemoria(tenantB, aziendaB.Id, "P-B"));
            db.Durc.AddRange(
                Durc(tenantA, aziendaA1.Id),
                Durc(tenantA, aziendaA2.Id),
                Durc(tenantB, aziendaB.Id));
            await db.SaveChangesAsync();

            var eliminati = await new AziendeService(db).EliminaBulkAsync(
                tenantA, [aziendaA1.Id, aziendaA2.Id, aziendaB.Id]);
            Assert.Equal(2, eliminati);
        }

        await using var verifica = NuovoContesto();
        Assert.All(
            await verifica.Promemoria.Where(p => p.TenantId == tenantA).ToListAsync(),
            p => Assert.Null(p.AziendaId));
        Assert.Equal(aziendaB.Id,
            (await verifica.Promemoria.SingleAsync(p => p.TenantId == tenantB)).AziendaId);
        Assert.Empty(await verifica.Durc.Where(d => d.TenantId == tenantA).ToListAsync());
        Assert.Single(await verifica.Durc.Where(d => d.TenantId == tenantB).ToListAsync());
        Assert.Single(await verifica.Aziende.Where(a => a.TenantId == tenantB).ToListAsync());
    }

    /// <summary>
    /// Scrive un dipendente direttamente in tabella, con le sole colonne che quella versione
    /// dello schema possiede davvero: stessa ragione degli altri helper legacy (una colonna
    /// nuova, come «Sesso», non deve rompere le prove sui vincoli tenant degli schemi storici).
    /// </summary>
    private static async Task InserisciDipendenteLegacyAsync(PagheDbContext db, DipendenteEntity d)
    {
        var valori = new Dictionary<string, object?>
        {
            ["Id"] = d.Id.ToString().ToUpperInvariant(),
            ["TenantId"] = d.TenantId.ToString().ToUpperInvariant(),
            ["CodiceFiscale"] = d.CodiceFiscale,
            ["Nome"] = d.Nome,
            ["Cognome"] = d.Cognome,
            ["Anonimizzato"] = d.Anonimizzato ? 1 : 0,
        };

        var presenti = new List<string>();
        var connessione = (SqliteConnection)db.Database.GetDbConnection();
        await using (var info = connessione.CreateCommand())
        {
            info.CommandText = "PRAGMA table_info(\"Dipendenti\");";
            await using var lettore = await info.ExecuteReaderAsync();
            while (await lettore.ReadAsync())
            {
                string colonna = lettore.GetString(1);
                if (valori.ContainsKey(colonna)) presenti.Add(colonna);
            }
        }

        await using var inserimento = connessione.CreateCommand();
        inserimento.CommandText =
            $"INSERT INTO \"Dipendenti\" ({string.Join(", ", presenti.Select(c => $"\"{c}\""))}) "
            + $"VALUES ({string.Join(", ", presenti.Select(c => "$" + c))});";
        foreach (var colonna in presenti)
            inserimento.Parameters.AddWithValue("$" + colonna, valori[colonna] ?? DBNull.Value);
        await inserimento.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Scrive un'azienda direttamente in tabella, con le sole colonne che quella versione dello
    /// schema possiede davvero: stessa ragione del rapporto legacy qui sotto (una colonna nuova
    /// sull'anagrafica non deve rompere le prove sui vincoli tenant degli schemi storici).
    /// </summary>
    private static async Task InserisciAziendaLegacyAsync(PagheDbContext db, AziendaEntity a)
    {
        var valori = new Dictionary<string, object?>
        {
            ["Id"] = a.Id.ToString().ToUpperInvariant(),
            ["TenantId"] = a.TenantId.ToString().ToUpperInvariant(),
            ["RagioneSociale"] = a.RagioneSociale,
            ["Indirizzo"] = a.Indirizzo,
            ["Cap"] = a.Cap,
            ["Comune"] = a.Comune,
            ["Provincia"] = a.Provincia,
            ["Nazione"] = a.Nazione,
            ["RegimeFiscale"] = a.RegimeFiscale,
            ["HaDipendenti"] = a.HaDipendenti ? 1 : 0,
            ["OltreQuindiciDipendenti"] = a.OltreQuindiciDipendenti ? 1 : 0,
            ["ContributiFondiSettore"] = a.ContributiFondiSettore.ToString(CultureInfo.InvariantCulture),
            ["AssoggettataIva"] = a.AssoggettataIva ? 1 : 0,
        };

        var presenti = new List<string>();
        var connessione = (SqliteConnection)db.Database.GetDbConnection();
        await using (var info = connessione.CreateCommand())
        {
            info.CommandText = "PRAGMA table_info(\"Aziende\");";
            await using var lettore = await info.ExecuteReaderAsync();
            while (await lettore.ReadAsync())
            {
                string colonna = lettore.GetString(1);
                if (valori.ContainsKey(colonna)) presenti.Add(colonna);
            }
        }

        await using var inserimento = connessione.CreateCommand();
        inserimento.CommandText =
            $"INSERT INTO \"Aziende\" ({string.Join(", ", presenti.Select(c => $"\"{c}\""))}) "
            + $"VALUES ({string.Join(", ", presenti.Select(c => "$" + c))});";
        foreach (var colonna in presenti)
            inserimento.Parameters.AddWithValue("$" + colonna, valori[colonna] ?? DBNull.Value);
        await inserimento.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Scrive un rapporto direttamente in tabella, con le sole colonne che quella versione
    /// dello schema possiede davvero. Salvarlo con il DbContext userebbe il modello di oggi:
    /// basterebbe una colonna aggiunta al dominio per far fallire una prova che riguarda i
    /// vincoli tenant e non l'anagrafica.
    /// </summary>
    private static async Task InserisciRapportoLegacyAsync(
        PagheDbContext db, RapportoLavoroEntity r)
    {
        var valori = new Dictionary<string, object?>
        {
            // EF Core scrive i GUID come testo maiuscolo: le chiavi esterne si confrontano
            // carattere per carattere, quindi la stessa forma va rispettata anche qui.
            ["Id"] = r.Id.ToString().ToUpperInvariant(),
            ["TenantId"] = r.TenantId.ToString().ToUpperInvariant(),
            ["AziendaId"] = r.AziendaId.ToString().ToUpperInvariant(),
            ["DipendenteId"] = r.DipendenteId.ToString().ToUpperInvariant(),
            ["CodiceCcnl"] = r.CodiceCcnl,
            ["Livello"] = r.Livello,
            ["Qualifica"] = r.Qualifica,
            ["TipoContratto"] = r.TipoContratto,
            ["OreSettimanali"] = r.OreSettimanali.ToString(CultureInfo.InvariantCulture),
            ["NumeroMensilita"] = r.NumeroMensilita,
            ["PartTime"] = r.PartTime ? 1 : 0,
            ["PercentualePartTime"] = r.PercentualePartTime.ToString(CultureInfo.InvariantCulture),
            ["ScattiAnzianita"] = r.ScattiAnzianita,
            ["Superminimo"] = r.Superminimo.ToString(CultureInfo.InvariantCulture),
            ["Regione"] = r.Regione,
            ["CodiceCatastaleComune"] = r.CodiceCatastaleComune,
            ["DetrazioniLavoroDipendente"] = r.DetrazioniLavoroDipendente ? 1 : 0,
            ["GiorniDetrazioneAnno"] = r.GiorniDetrazioneAnno,
            ["DataAssunzione"] = r.DataAssunzione.ToString("yyyy-MM-dd"),
            ["FerieResidueIniziali"] = r.FerieResidueIniziali.ToString(CultureInfo.InvariantCulture),
            ["PermessiResiduiIniziali"] = r.PermessiResiduiIniziali.ToString(CultureInfo.InvariantCulture),
        };

        var presenti = new List<string>();
        var connessione = (SqliteConnection)db.Database.GetDbConnection();
        await using (var info = connessione.CreateCommand())
        {
            info.CommandText = "PRAGMA table_info(\"Rapporti\");";
            await using var lettore = await info.ExecuteReaderAsync();
            while (await lettore.ReadAsync())
            {
                string colonna = lettore.GetString(1);
                if (valori.ContainsKey(colonna)) presenti.Add(colonna);
            }
        }

        await using var inserimento = connessione.CreateCommand();
        inserimento.CommandText =
            $"INSERT INTO \"Rapporti\" ({string.Join(", ", presenti.Select(c => $"\"{c}\""))}) "
            + $"VALUES ({string.Join(", ", presenti.Select(c => "$" + c))});";
        foreach (var colonna in presenti)
            inserimento.Parameters.AddWithValue("$" + colonna, valori[colonna] ?? DBNull.Value);
        await inserimento.ExecuteNonQueryAsync();
    }

    private static async Task AssertFkAsync(PagheDbContext db, object entita)
    {
        db.Add(entita);
        var errore = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Equal(19, Assert.IsType<SqliteException>(errore.InnerException).SqliteErrorCode);
        db.ChangeTracker.Clear();
    }

    private async Task<Guid> InserisciOrfanoAsync(PagheDbContext db, string tabella)
    {
        var id = Guid.NewGuid();
        await db.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = 0;");
        try
        {
            if (tabella == "Rapporti")
            {
                var rapporto = Rapporto(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
                rapporto.Id = id;
                await InserisciRapportoLegacyAsync(db, rapporto);
            }
            else if (tabella == "Durc")
            {
                var durc = Durc(Guid.NewGuid(), Guid.NewGuid());
                durc.Id = id;
                db.Durc.Add(durc);
            }
            else if (tabella == "Promemoria")
            {
                var promemoria = Promemoria(Guid.NewGuid(), Guid.NewGuid(), "Orfano");
                promemoria.Id = id;
                db.Promemoria.Add(promemoria);
            }
            else throw new ArgumentOutOfRangeException(nameof(tabella));
            await db.SaveChangesAsync();
        }
        finally
        {
            await db.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = 1;");
        }
        return id;
    }

    private bool RigaEsiste(string tabella, Guid id)
    {
        Assert.Contains(tabella, new[] { "Rapporti", "Durc", "Promemoria" });
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = $"SELECT COUNT(*) FROM \"{tabella}\" WHERE \"Id\" = $id;";
        cmd.Parameters.AddWithValue("$id", id);
        return Convert.ToInt64(cmd.ExecuteScalar()) == 1;
    }

    private static string RimuoviForeignKey(string ddl)
    {
        var risultato = Regex.Replace(
            ddl,
            @",\s*CONSTRAINT\s+""FK_[^""]+""\s+FOREIGN KEY\s*\([^)]*\)\s+"
                + @"REFERENCES\s+""(?:Aziende|Dipendenti)""\s*\([^)]*\)"
                + @"(?:\s+ON DELETE\s+(?:CASCADE|SET NULL|RESTRICT|NO ACTION))?",
            "",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
        Assert.NotEqual(ddl, risultato);
        return risultato;
    }

    private void RicostruisciTabella(string tabella, Func<string, string> trasformaDdl)
    {
        Assert.Contains(tabella, new[] { "Rapporti", "Durc", "Promemoria" });
        string ddl;
        using (var leggi = _connection.CreateCommand())
        {
            leggi.CommandText =
                "SELECT sql FROM sqlite_master WHERE type = 'table' AND name = $tabella;";
            leggi.Parameters.AddWithValue("$tabella", tabella);
            ddl = Assert.IsType<string>(leggi.ExecuteScalar());
        }
        var temporanea = $"__Test_{tabella}";
        var ddlTrasformato = trasformaDdl(ddl).Replace(
            $"CREATE TABLE \"{tabella}\"",
            $"CREATE TABLE \"{temporanea}\"",
            StringComparison.Ordinal);
        Assert.NotEqual(ddl, ddlTrasformato);

        var colonne = new List<string>();
        using (var leggiColonne = _connection.CreateCommand())
        {
            leggiColonne.CommandText = $"SELECT name FROM pragma_table_info('{tabella}') ORDER BY cid;";
            using var reader = leggiColonne.ExecuteReader();
            while (reader.Read()) colonne.Add(reader.GetString(0));
        }
        var elenco = string.Join(", ", colonne.Select(c => $"\"{c}\""));

        using var disabilita = _connection.CreateCommand();
        disabilita.CommandText = "PRAGMA foreign_keys = 0;";
        disabilita.ExecuteNonQuery();
        try
        {
            using var ricostruisci = _connection.CreateCommand();
            ricostruisci.CommandText =
                $"""
                {ddlTrasformato};
                INSERT INTO "{temporanea}" ({elenco}) SELECT {elenco} FROM "{tabella}";
                DROP TABLE "{tabella}";
                ALTER TABLE "{temporanea}" RENAME TO "{tabella}";
                """;
            ricostruisci.ExecuteNonQuery();
        }
        finally
        {
            using var abilita = _connection.CreateCommand();
            abilita.CommandText = "PRAGMA foreign_keys = 1;";
            abilita.ExecuteNonQuery();
        }
    }

    private void AssertVincoliTenantInstallati()
    {
        Assert.True(HaVincoloTenant("Rapporti", "Dipendenti", "DipendenteId", "CASCADE"));
        Assert.True(HaVincoloTenant("Durc", "Aziende", "AziendaId", "CASCADE"));
        Assert.True(HaVincoloTenant("Promemoria", "Aziende", "AziendaId", "RESTRICT"));
    }

    private bool HaChiaveTenantNonParziale(string tabella)
    {
        Assert.Contains(tabella, new[] { "Aziende", "Dipendenti" });
        using var cmd = _connection.CreateCommand();
        cmd.CommandText =
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
            );
            """;
        return Convert.ToInt64(cmd.ExecuteScalar()) == 1;
    }

    private bool HaVincoloTenant(
        string figlia, string padre, string colonnaFiglia, string azione)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText =
            $"""
            SELECT COUNT(*)
            FROM (
                SELECT id
                FROM pragma_foreign_key_list('{figlia}')
                WHERE "table" = '{padre}' AND UPPER("on_delete") = '{azione}'
                GROUP BY id
                HAVING COUNT(*) = 2
                   AND SUM(CASE WHEN "from" = 'TenantId' AND "to" = 'TenantId' THEN 1 ELSE 0 END) = 1
                   AND SUM(CASE WHEN "from" = '{colonnaFiglia}' AND "to" = 'Id' THEN 1 ELSE 0 END) = 1
            );
            """;
        return Convert.ToInt64(cmd.ExecuteScalar()) == 1;
    }

    private async Task<bool> TabellaEsisteAsync(string nome)
    {
        await using var cmd = _connection.CreateCommand();
        cmd.CommandText =
            "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $nome;";
        cmd.Parameters.AddWithValue("$nome", nome);
        return Convert.ToInt64(await cmd.ExecuteScalarAsync()) > 0;
    }
}
