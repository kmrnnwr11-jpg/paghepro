using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Paghe.Persistence;
using Paghe.Persistence.Entities;
using Paghe.Persistence.Gdpr;
using Xunit;

namespace Paghe.Tests;

/// <summary>
/// Test dell'audit log (GDPR): ogni inserimento/modifica/cancellazione di un'entità tenant-scoped
/// produce una riga di audit con chi/cosa/quando; le entità senza tenant non sono auditate; e i
/// valori sensibili (IBAN) non finiscono mai nell'audit.
/// </summary>
public sealed class AuditLogTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public AuditLogTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        using var db = NuovoContesto();
        db.Database.EnsureCreated();
    }

    private PagheDbContext NuovoContesto() =>
        new(new DbContextOptionsBuilder<PagheDbContext>().UseSqlite(_connection).Options);

    [Fact]
    public async Task Inserimento_RegistraChiCosaQuando()
    {
        var tenant = Guid.NewGuid();
        var utente = Guid.NewGuid();

        using (AmbientContestoAudit.Imposta(tenant, utente))
        await using (var db = NuovoContesto())
        {
            db.Aziende.Add(new AziendaEntity { Id = Guid.NewGuid(), TenantId = tenant, RagioneSociale = "Alfa Srl" });
            await db.SaveChangesAsync();
        }

        await using (var db = NuovoContesto())
        {
            var audit = await db.AuditLog.Where(a => a.TenantId == tenant).ToListAsync();
            var riga = Assert.Single(audit); // solo l'azienda (tenant-scoped); nessun rumore
            Assert.Equal("Inserimento", riga.Azione);
            Assert.Equal("Aziende", riga.Entita);
            Assert.Equal(utente, riga.UtenteId);
            Assert.Null(riga.CampiModificati); // i nomi dei campi si registrano solo sulle modifiche
            Assert.True(riga.QuandoUtc <= DateTime.UtcNow);
        }
    }

    [Fact]
    public async Task Modifica_RegistraINomiDeiCampiCambiati()
    {
        var tenant = Guid.NewGuid();
        var aziendaId = Guid.NewGuid();

        await using (var db = NuovoContesto())
        {
            db.Aziende.Add(new AziendaEntity { Id = aziendaId, TenantId = tenant, RagioneSociale = "Alfa Srl" });
            await db.SaveChangesAsync();
        }
        await using (var db = NuovoContesto())
        {
            var az = await db.Aziende.FirstAsync(a => a.Id == aziendaId);
            az.RagioneSociale = "Alfa Beta Srl";
            await db.SaveChangesAsync();
        }

        await using (var db = NuovoContesto())
        {
            var modifica = await db.AuditLog.SingleAsync(a => a.TenantId == tenant && a.Azione == "Modifica");
            Assert.Equal("Aziende", modifica.Entita);
            Assert.Equal(aziendaId.ToString(), modifica.ChiaveEntita);
            Assert.Contains("RagioneSociale", modifica.CampiModificati);
        }
    }

    [Fact]
    public async Task IbanDelDipendente_NonCompareMaiNellAudit()
    {
        const string iban = "IT60X0542811101000000123456";
        var tenant = Guid.NewGuid();

        using (AmbientContestoAudit.Imposta(tenant, Guid.NewGuid()))
        await using (var db = NuovoContesto())
        {
            db.Dipendenti.Add(new DipendenteEntity
            {
                Id = Guid.NewGuid(), TenantId = tenant,
                CodiceFiscale = "RSSMRA80A01F205X", Nome = "Mario", Cognome = "Rossi", Iban = iban,
            });
            await db.SaveChangesAsync();
        }

        await using (var db = NuovoContesto())
        {
            var audit = await db.AuditLog.Where(a => a.TenantId == tenant).ToListAsync();
            var riga = Assert.Single(audit);
            Assert.Equal("Dipendenti", riga.Entita);
            // GDPR: nessun campo dell'audit contiene il valore IBAN.
            foreach (var testo in new[] { riga.ChiaveEntita, riga.CampiModificati, riga.Entita })
                Assert.DoesNotContain(iban, testo ?? "");
        }
    }

    public void Dispose() => _connection.Dispose();
}
