using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Paghe.Persistence;
using Paghe.Persistence.Entities;
using Xunit;

namespace Paghe.Tests;

/// <summary>
/// Test di <see cref="SchemaDatabase"/>: lo schema è gestito dalle migrazioni EF versionate e i
/// database creati prima delle migrazioni (con <c>EnsureCreated</c> e DDL scritto a mano) vengono
/// adottati senza perdere dati.
/// </summary>
public sealed class MigrazioniSchemaTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public MigrazioniSchemaTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
    }

    // Le migrazioni SQLite vivono in un assembly dedicato (EF ne ammette uno solo per provider):
    // la stessa configurazione usata dall'host in Program.cs.
    private PagheDbContext NuovoContesto() => new(
        new DbContextOptionsBuilder<PagheDbContext>()
            .UseSqlite(_connection, sql => sql.MigrationsAssembly("Paghe.Migrazioni.Sqlite"))
            .Options);

    private List<string> Tabelle()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table';";
        using var reader = cmd.ExecuteReader();
        var nomi = new List<string>();
        while (reader.Read()) nomi.Add(reader.GetString(0));
        return nomi;
    }

    private List<string> MigrazioniApplicate()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = $"SELECT \"MigrationId\" FROM \"{HistoryRepository.DefaultTableName}\";";
        using var reader = cmd.ExecuteReader();
        var righe = new List<string>();
        while (reader.Read()) righe.Add(reader.GetString(0));
        return righe;
    }

    /// <summary>
    /// Lo storico deve contenere esattamente le migrazioni dell'assembly, quali che siano.
    /// Confrontare con l'elenco reale invece che con un numero fisso: così il test non si rompe
    /// quando si aggiunge una migrazione, ma continua ad accorgersi se una resta indietro.
    /// </summary>
    private void AssertSchemaAllineato()
    {
        using var db = NuovoContesto();
        Assert.Equal(
            db.Database.GetMigrations().OrderBy(m => m, StringComparer.Ordinal),
            MigrazioniApplicate().OrderBy(m => m, StringComparer.Ordinal));
    }

    [Fact]
    public void DatabaseVuoto_LeMigrazioniCreanoLoSchemaELoRegistrano()
    {
        using var db = NuovoContesto();

        SchemaDatabase.Applica(db);

        Assert.Contains("Aziende", Tabelle());
        Assert.Contains("PolicyRetention", Tabelle());
        AssertSchemaAllineato();
    }

    [Fact]
    public void SchemaPreesistenteSenzaStorico_VieneAdottatoSenzaPerdereDati()
    {
        // Database "storico": creato con EnsureCreated, quindi senza tabella delle migrazioni.
        using (var db = NuovoContesto())
        {
            db.Database.EnsureCreated();
            db.Aziende.Add(new AziendaEntity
            {
                TenantId = Guid.NewGuid(),
                RagioneSociale = "Alfa Srl",
                PartitaIva = "12345678903",
            });
            db.SaveChanges();
        }
        Assert.DoesNotContain(HistoryRepository.DefaultTableName, Tabelle());

        using (var db = NuovoContesto())
        {
            var esito = SchemaDatabase.Applica(db);
            Assert.Contains("adottato", esito);
        }

        AssertSchemaAllineato();
        using var verifica = NuovoContesto();
        Assert.Equal("Alfa Srl", Assert.Single(verifica.Aziende).RagioneSociale);
    }

    [Fact]
    public void SchemaPreesistenteIncompleto_LeTabelleMancantiVengonoRicreate()
    {
        using (var db = NuovoContesto())
        {
            db.Database.EnsureCreated();
            // Simula un database fermo a una versione precedente del modello.
            db.Database.ExecuteSqlRaw("DROP TABLE \"PraticheInail\";");
            db.Database.ExecuteSqlRaw("DROP INDEX \"IX_Persone_TenantId_CodiceFiscale\";");
        }
        Assert.DoesNotContain("PraticheInail", Tabelle());

        using (var db = NuovoContesto())
            Assert.Contains("PraticheInail", SchemaDatabase.Applica(db));

        Assert.Contains("PraticheInail", Tabelle());
        AssertSchemaAllineato();

        using var cmd = _connection.CreateCommand();
        cmd.CommandText =
            "SELECT COUNT(*) FROM sqlite_master WHERE type = 'index' " +
            "AND name = 'IX_Persone_TenantId_CodiceFiscale';";
        Assert.Equal(1L, Convert.ToInt64(cmd.ExecuteScalar()));
    }

    [Fact]
    public void SecondaEsecuzione_NonRipeteNulla()
    {
        using (var db = NuovoContesto()) SchemaDatabase.Applica(db);

        using (var db = NuovoContesto())
            Assert.Equal("schema già aggiornato", SchemaDatabase.Applica(db));

        AssertSchemaAllineato();
    }

    public void Dispose() => _connection.Dispose();
}
