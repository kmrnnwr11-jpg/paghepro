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

    [Fact]
    public async Task ModificaIban_RegistraSoloNomeSemanticoSenzaValoreOCampoTecnico()
    {
        const string ibanNuovo = "IT91A0300203280123456789012";
        var tenant = Guid.NewGuid();
        var dipendenteId = Guid.NewGuid();

        await using (var db = NuovoContesto())
        {
            db.Dipendenti.Add(new DipendenteEntity
            {
                Id = dipendenteId,
                TenantId = tenant,
                CodiceFiscale = "VRDLGI90A01F205Z",
                Nome = "Luigi",
                Cognome = "Verdi",
                Iban = "IT60X0542811101000000123456",
            });
            await db.SaveChangesAsync();
        }

        using (AmbientContestoAudit.Imposta(tenant, Guid.NewGuid(), "trace-iban"))
        await using (var db = NuovoContesto())
        {
            var dipendente = await db.Dipendenti.SingleAsync(d => d.Id == dipendenteId);
            dipendente.Iban = ibanNuovo;
            await db.SaveChangesAsync();
        }

        await using (var db = NuovoContesto())
        {
            var modifica = await db.AuditLog.SingleAsync(a =>
                a.TenantId == tenant && a.Azione == "Modifica");
            Assert.Equal("Iban", modifica.CampiModificati);
            Assert.Equal("trace-iban", modifica.CorrelationId);
            Assert.DoesNotContain("Protetto", modifica.CampiModificati ?? "");
            Assert.DoesNotContain(ibanNuovo, modifica.CampiModificati ?? "");
        }
    }

    [Fact]
    public async Task TenantAmbientDiversoDalRecord_BloccaOperazioneEScritturaAudit()
    {
        var tenantAutenticato = Guid.NewGuid();
        var tenantDelRecord = Guid.NewGuid();

        using (AmbientContestoAudit.Imposta(tenantAutenticato, Guid.NewGuid()))
        await using (var db = NuovoContesto())
        {
            db.Aziende.Add(new AziendaEntity
            {
                Id = Guid.NewGuid(),
                TenantId = tenantDelRecord,
                RagioneSociale = "Tenant errato",
            });
            var errore = await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
            Assert.Contains("tenant", errore.Message, StringComparison.OrdinalIgnoreCase);
        }

        await using (var db = NuovoContesto())
        {
            Assert.Empty(await db.Aziende.ToListAsync());
            Assert.Empty(await db.AuditLog.ToListAsync());
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Registro_ModificaOCancellazioneViaEf_VieneBloccata(bool modifica)
    {
        var id = Guid.NewGuid();
        await using (var db = NuovoContesto())
        {
            db.AuditLog.Add(new AuditLogEntity
            {
                Id = id,
                TenantId = Guid.NewGuid(),
                Categoria = "Accesso",
                Azione = "Lettura",
                Entita = "Aziende",
                QuandoUtc = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        await using (var db = NuovoContesto())
        {
            var riga = await db.AuditLog.SingleAsync(a => a.Id == id);
            if (modifica) riga.Esito = "Alterato";
            else db.AuditLog.Remove(riga);
            var errore = await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
            Assert.Contains("append-only", errore.Message);
        }
    }

    [Fact]
    public async Task Consultazione_FiltraSempreTenantERisolveSoloUtentiDelloStessoTenant()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var utenteA = Guid.NewGuid();
        var stessoIdNelLogB = utenteA;

        await using (var db = NuovoContesto())
        {
            db.Utenti.Add(new UtenteEntity
            {
                Id = utenteA,
                TenantId = tenantA,
                Email = "admin-a@test.local",
                PasswordHash = "x",
            });
            await db.SaveChangesAsync();
            await db.Database.ExecuteSqlRawAsync("DELETE FROM \"AuditLog\"");
            db.AuditLog.AddRange(
                new AuditLogEntity
                {
                    Id = Guid.NewGuid(), TenantId = tenantA, UtenteId = utenteA,
                    Categoria = "Accesso", Azione = "Lettura", Entita = "Aziende",
                    QuandoUtc = DateTime.UtcNow,
                },
                new AuditLogEntity
                {
                    Id = Guid.NewGuid(), TenantId = tenantB, UtenteId = stessoIdNelLogB,
                    Categoria = "Accesso", Azione = "Lettura", Entita = "Dipendenti",
                    QuandoUtc = DateTime.UtcNow,
                });
            await db.SaveChangesAsync();
        }

        await using (var db = NuovoContesto())
        {
            var servizio = new AuditLogService(db);
            var paginaA = await servizio.ElencaAsync(tenantA);
            var voceA = Assert.Single(paginaA.Voci);
            Assert.Equal("admin-a@test.local", voceA.Utente);
            Assert.Equal("Aziende", voceA.Entita);

            var paginaB = await servizio.ElencaAsync(tenantB);
            var voceB = Assert.Single(paginaB.Voci);
            Assert.NotEqual("admin-a@test.local", voceB.Utente);
            Assert.Equal("Dipendenti", voceB.Entita);
        }
    }

    [Fact]
    public async Task TransazioneAnnullata_NonLasciaNeDatoNeEventoDiModifica()
    {
        var tenant = Guid.NewGuid();
        await using (var db = NuovoContesto())
        await using (var transazione = await db.Database.BeginTransactionAsync())
        {
            db.Aziende.Add(new AziendaEntity
            {
                Id = Guid.NewGuid(),
                TenantId = tenant,
                RagioneSociale = "Da annullare",
            });
            await db.SaveChangesAsync();
            Assert.Single(await db.AuditLog.Where(a => a.TenantId == tenant).ToListAsync());
            await transazione.RollbackAsync();
        }

        await using (var db = NuovoContesto())
        {
            Assert.Empty(await db.Aziende.Where(a => a.TenantId == tenant).ToListAsync());
            Assert.Empty(await db.AuditLog.Where(a => a.TenantId == tenant).ToListAsync());
        }
    }

    [Fact]
    public async Task SchemaAuditPreesistente_VieneAllineatoELeRigheLegacySonoClassificate()
    {
        await using var connessione = new SqliteConnection("DataSource=:memory:");
        await connessione.OpenAsync();
        await using (var comando = connessione.CreateCommand())
        {
            comando.CommandText = """
                CREATE TABLE "AuditLog" (
                    "Id" TEXT NOT NULL PRIMARY KEY,
                    "TenantId" TEXT NOT NULL,
                    "UtenteId" TEXT NULL,
                    "Azione" TEXT NOT NULL,
                    "Entita" TEXT NOT NULL,
                    "ChiaveEntita" TEXT NOT NULL,
                    "CampiModificati" TEXT NULL,
                    "QuandoUtc" TEXT NOT NULL
                );
                INSERT INTO "AuditLog"
                    ("Id", "TenantId", "UtenteId", "Azione", "Entita", "ChiaveEntita", "CampiModificati", "QuandoUtc")
                VALUES
                    ('00000000-0000-0000-0000-000000000001',
                     '00000000-0000-0000-0000-000000000002', NULL,
                     'Modifica', 'Aziende', 'record', 'RagioneSociale', '2026-07-15 10:00:00');
                """;
            await comando.ExecuteNonQueryAsync();
        }

        var opzioni = new DbContextOptionsBuilder<PagheDbContext>()
            .UseSqlite(connessione)
            .Options;
        await using (var db = new PagheDbContext(opzioni))
        {
            db.AllineaColonneMancanti();
            var legacy = await db.AuditLog.AsNoTracking().SingleAsync();
            Assert.Equal("ModificaDati", legacy.Categoria);
            Assert.Null(legacy.MetodoHttp);
            Assert.Null(legacy.StatusCode);
        }
    }

    [Fact]
    public async Task RetryDopoDbUpdateException_NonDuplicaGliEventiGenerati()
    {
        var tenant = Guid.NewGuid();
        var emailDuplicata = $"duplicata-{Guid.NewGuid():N}@test.local";

        using (AmbientContestoAudit.Imposta(tenant, Guid.NewGuid(), "trace-retry"))
        await using (var db = NuovoContesto())
        {
            db.Tenants.Add(new TenantEntity
            {
                Id = tenant,
                RagioneSociale = "Studio retry",
                CreatoIl = DateTime.UtcNow,
            });
            var primo = new UtenteEntity
            {
                Id = Guid.NewGuid(), TenantId = tenant, Email = emailDuplicata,
                PasswordHash = "x", Ruolo = "admin", Attivo = true, CreatoIl = DateTime.UtcNow,
            };
            var secondo = new UtenteEntity
            {
                Id = Guid.NewGuid(), TenantId = tenant, Email = emailDuplicata,
                PasswordHash = "x", Ruolo = "operatore", Attivo = true, CreatoIl = DateTime.UtcNow,
            };
            db.Utenti.AddRange(primo, secondo);

            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
            secondo.Email = $"corretta-{Guid.NewGuid():N}@test.local";
            await db.SaveChangesAsync();
        }

        await using (var db = NuovoContesto())
        {
            var eventi = await db.AuditLog
                .Where(a => a.TenantId == tenant && a.Azione == "Inserimento")
                .ToListAsync();
            Assert.Equal(2, eventi.Count);
            Assert.All(eventi, e => Assert.Equal("trace-retry", e.CorrelationId));
        }
    }

    [Fact]
    public async Task CambioTenantDiUnRecordEsistente_VieneSempreBloccato()
    {
        var tenantOriginale = Guid.NewGuid();
        var tenantNuovo = Guid.NewGuid();
        var aziendaId = Guid.NewGuid();
        await using (var db = NuovoContesto())
        {
            db.Aziende.Add(new AziendaEntity
            {
                Id = aziendaId,
                TenantId = tenantOriginale,
                RagioneSociale = "Non spostabile",
            });
            await db.SaveChangesAsync();
        }

        using (AmbientContestoAudit.Imposta(tenantNuovo, Guid.NewGuid()))
        await using (var db = NuovoContesto())
        {
            var azienda = await db.Aziende.SingleAsync(a => a.Id == aziendaId);
            azienda.TenantId = tenantNuovo;
            await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        }

        await using (var db = NuovoContesto())
        {
            Assert.Equal(tenantOriginale,
                (await db.Aziende.AsNoTracking().SingleAsync(a => a.Id == aziendaId)).TenantId);
            Assert.DoesNotContain(await db.AuditLog.ToListAsync(),
                a => a.Azione == "Modifica" && a.ChiaveEntita == aziendaId.ToString());
        }
    }

    [Fact]
    public async Task SospensioneStudio_RegistraEventoPiattaformaNelTenantDelSuperadmin()
    {
        var tenantSuperadmin = Guid.NewGuid();
        var tenantBersaglio = Guid.NewGuid();
        var utenteSuperadmin = Guid.NewGuid();
        await using (var db = NuovoContesto())
        {
            db.Tenants.AddRange(
                new TenantEntity { Id = tenantSuperadmin, RagioneSociale = "Piattaforma", CreatoIl = DateTime.UtcNow },
                new TenantEntity { Id = tenantBersaglio, RagioneSociale = "Studio bersaglio", CreatoIl = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }

        using (AmbientContestoAudit.Imposta(tenantSuperadmin, utenteSuperadmin, "trace-piattaforma"))
        await using (var db = NuovoContesto())
        {
            var bersaglio = await db.Tenants.SingleAsync(t => t.Id == tenantBersaglio);
            bersaglio.Sospeso = true;
            await db.SaveChangesAsync();
        }

        await using (var db = NuovoContesto())
        {
            var evento = await db.AuditLog.SingleAsync(a => a.CorrelationId == "trace-piattaforma");
            Assert.Equal(tenantSuperadmin, evento.TenantId);
            Assert.Equal(utenteSuperadmin, evento.UtenteId);
            Assert.Equal("Tenants", evento.Entita);
            Assert.Equal(tenantBersaglio.ToString(), evento.ChiaveEntita);
            Assert.Contains("Sospeso", evento.CampiModificati);
        }
    }

    [Fact]
    public async Task Consultazione_NormalizzaDateEFiltroSenzaKindComeUtc()
    {
        var tenant = Guid.NewGuid();
        var istante = new DateTime(2026, 7, 15, 10, 30, 0, DateTimeKind.Utc);
        await using (var db = NuovoContesto())
        {
            db.AuditLog.Add(new AuditLogEntity
            {
                Id = Guid.NewGuid(), TenantId = tenant, Categoria = "Accesso",
                Azione = "Lettura", Entita = "Aziende", QuandoUtc = istante,
            });
            await db.SaveChangesAsync();
        }

        await using (var db = NuovoContesto())
        {
            var servizio = new AuditLogService(db);
            var daSenzaKind = new DateTime(2026, 7, 15, 10, 0, 0, DateTimeKind.Unspecified);
            var pagina = await servizio.ElencaAsync(tenant, daUtc: daSenzaKind);
            var voce = Assert.Single(pagina.Voci);
            Assert.Equal(DateTimeKind.Utc, voce.QuandoUtc.Kind);
            Assert.Equal(istante, voce.QuandoUtc);
        }
    }

    public void Dispose() => _connection.Dispose();
}
