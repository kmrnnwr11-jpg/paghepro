using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Paghe.Core.AdeTelematico;
using Paghe.Core.Domain;
using Paghe.Persistence;
using Paghe.Persistence.Entities;

namespace Paghe.Tests.AdeTelematico;

/// <summary>
/// Integrazione su SQLite: l'F24 del mese deve raccogliere i cedolini archiviati dell'azienda
/// e aggregare IRPEF, addizionali e contributi INPS.
/// </summary>
public sealed class F24DelMeseDbTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public F24DelMeseDbTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        using var db = NuovoContesto();
        db.Database.EnsureCreated();
    }

    private PagheDbContext NuovoContesto() =>
        new(new DbContextOptionsBuilder<PagheDbContext>().UseSqlite(_connection).Options);

    [Fact]
    public async Task Genera_f24_del_mese_da_cedolini_archiviati()
    {
        var tenant = Guid.NewGuid();
        var aziendaId = Guid.NewGuid();
        var rapporto1 = Guid.NewGuid();
        var rapporto2 = Guid.NewGuid();

        var dipendente1 = Guid.NewGuid();
        var dipendente2 = Guid.NewGuid();

        await using (var db = NuovoContesto())
        {
            db.Aziende.Add(new AziendaEntity
            {
                Id = aziendaId, TenantId = tenant, RagioneSociale = "Acme Srl",
                PartitaIva = "01234560017", CodiceFiscale = "01234560017",
                Indirizzo = "Via Roma 1", Cap = "20100", Comune = "Milano", Provincia = "MI",
                Settore = "COMMERCIO_SERVIZI", CodiceAteco = "47.91.10",
                MatricolaInps = "1234567890", CodiceSedeInps = "7100", CscInps = "10101",
                PatInail = "12345678/90", CcnlDefault = "COMMERCIO_TERZIARIO", LivelloDefault = "3",
                // Profilo contributivo completo (stesse aliquote del seed: i totali non cambiano).
                CodiciAutorizzazioneInps = "NESSUNO", CodiceFondoIntegrazioneSalariale = "NESSUNO",
                CodiceAgevolazioneContributiva = "NESSUNA", AliquotaInpsDipendente = 0.0919m,
                AliquotaInpsDatore = 0.2788m, FonteProfiloContributivo = "Tabella di esempio (test)",
            });
            db.Dipendenti.AddRange(
                new DipendenteEntity { Id = dipendente1, TenantId = tenant, CodiceFiscale = "RSSMRA85T10A562S", Nome = "Mario", Cognome = "Rossi" },
                new DipendenteEntity { Id = dipendente2, TenantId = tenant, CodiceFiscale = "VRDLGI80A01F205X", Nome = "Luigi", Cognome = "Verdi" });
            db.Rapporti.AddRange(
                Rapporto(rapporto1, tenant, aziendaId, dipendente1),
                Rapporto(rapporto2, tenant, aziendaId, dipendente2));
            db.Cedolini.AddRange(
                Cedolino(tenant, aziendaId, rapporto1, irpef: 300m, addReg: 20m, addCom: 10m, inpsDip: 150m, inpsDat: 450m),
                Cedolino(tenant, aziendaId, rapporto2, irpef: 200m, addReg: 15m, addCom: 5m, inpsDip: 100m, inpsDat: 300m));
            await db.SaveChangesAsync();
            foreach (var cedolino in db.Cedolini.Local)
                await db.ClassificaPreWorkflowAsync(tenant, cedolino.Id);
        }

        await using (var db = NuovoContesto())
        {
            var svc = new CedoliniService(db, new ParametriNormativiService(db));
            var f24 = await svc.GeneraF24DelMeseAsync(tenant, aziendaId, 2026, 6, matricolaInps: "1234567890", codiceSedeInps: "7100");

            Assert.Equal(500m, f24.Erario.Single(e => e.CodiceTributo == "1001").ImportoDebito);
            Assert.Equal(35m, f24.Regioni.Single().ImportoDebito);
            Assert.Equal(15m, f24.TributiLocali.Single().ImportoDebito);
            Assert.Equal(1000m, f24.Inps.Single().ImportoDebito);
            Assert.Equal("06/2026", f24.Inps.Single().PeriodoDa);
            Assert.True(ValidazioneF24.Verifica(f24).Valido);
        }
    }

    [Fact]
    public async Task Senza_cedolini_del_mese_segnala()
    {
        var tenant = Guid.NewGuid();
        var aziendaId = Guid.NewGuid();
        await using (var db = NuovoContesto())
        {
            db.Aziende.Add(new AziendaEntity
            {
                Id = aziendaId, TenantId = tenant, RagioneSociale = "Acme Srl", PartitaIva = "01234560017",
                Indirizzo = "Via Roma 1", Cap = "20100", Comune = "Milano", Provincia = "MI",
                Settore = "COMMERCIO_SERVIZI", CodiceAteco = "47.91.10",
                MatricolaInps = "1234567890", CodiceSedeInps = "7100", CscInps = "10101",
                PatInail = "12345678/90", CcnlDefault = "COMMERCIO_TERZIARIO", LivelloDefault = "3",
                // Profilo contributivo completo (stesse aliquote del seed: i totali non cambiano).
                CodiciAutorizzazioneInps = "NESSUNO", CodiceFondoIntegrazioneSalariale = "NESSUNO",
                CodiceAgevolazioneContributiva = "NESSUNA", AliquotaInpsDipendente = 0.0919m,
                AliquotaInpsDatore = 0.2788m, FonteProfiloContributivo = "Tabella di esempio (test)",
            });
            await db.SaveChangesAsync();
        }

        await using (var db = NuovoContesto())
        {
            var svc = new CedoliniService(db, new ParametriNormativiService(db));
            await Assert.ThrowsAsync<ArgumentException>(() =>
                svc.GeneraF24DelMeseAsync(tenant, aziendaId, 2026, 6, "1234567890"));
        }
    }

    [Fact]
    public async Task CedolinoInBozza_NonAlimentaF24()
    {
        var tenant = Guid.NewGuid();
        var aziendaId = Guid.NewGuid();
        await using var db = NuovoContesto();
        db.Aziende.Add(new AziendaEntity
        {
            Id = aziendaId, TenantId = tenant, RagioneSociale = "Acme Srl",
            PartitaIva = "01234560017", CodiceFiscale = "01234560017",
            Indirizzo = "Via Roma 1", Cap = "20100", Comune = "Milano", Provincia = "MI",
            Settore = "COMMERCIO_SERVIZI", CodiceAteco = "47.91.10",
            MatricolaInps = "1234567890", CodiceSedeInps = "7100", CscInps = "10101",
            PatInail = "12345678/90", CcnlDefault = "COMMERCIO_TERZIARIO", LivelloDefault = "3",
            // Profilo contributivo completo (stesse aliquote del seed: i totali non cambiano).
            CodiciAutorizzazioneInps = "NESSUNO", CodiceFondoIntegrazioneSalariale = "NESSUNO",
            CodiceAgevolazioneContributiva = "NESSUNA", AliquotaInpsDipendente = 0.0919m,
            AliquotaInpsDatore = 0.2788m, FonteProfiloContributivo = "Tabella di esempio (test)",
        });
        db.Cedolini.Add(new CedolinoEntity
        {
            Id = Guid.NewGuid(), TenantId = tenant, AziendaId = aziendaId,
            RapportoLavoroId = Guid.NewGuid(), Anno = 2026, Mese = 6,
            Stato = nameof(StatoCedolino.Bozza), IrpefNetta = 999m,
        });
        await db.SaveChangesAsync();

        var errore = await Assert.ThrowsAsync<ArgumentException>(() =>
            new CedoliniService(db, new ParametriNormativiService(db))
                .GeneraF24DelMeseAsync(tenant, aziendaId, 2026, 6, "1234567890"));
        Assert.Contains("cedolino chiuso", errore.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AziendaProvvisoria_NonGeneraF24AncheSeLaRequestInviaUnaMatricola()
    {
        var tenant = Guid.NewGuid();
        var aziendaId = Guid.NewGuid();
        await using (var db = NuovoContesto())
        {
            db.Aziende.Add(new AziendaEntity
            {
                Id = aziendaId, TenantId = tenant, RagioneSociale = "Azienda Prova Srl",
                PartitaIva = "01987654322", Indirizzo = "Via Test 1", Cap = "20100",
                Comune = "Milano", Provincia = "MI", Settore = "AGRICOLTURA", CodiceAteco = "01.46.00",
            });
            await db.SaveChangesAsync();
        }

        await using (var db = NuovoContesto())
        {
            var svc = new CedoliniService(db, new ParametriNormativiService(db));
            var errore = await Assert.ThrowsAsync<ArgumentException>(() =>
                svc.GeneraF24DelMeseAsync(tenant, aziendaId, 2026, 6, "000000"));
            Assert.Contains("dati contributivi provvisori", errore.Message);
        }
    }

    [Fact]
    public async Task AziendaProvvisoria_NonGeneraUniEmensAncheSeLaRequestInviaUnaMatricola()
    {
        var tenant = Guid.NewGuid();
        var aziendaId = Guid.NewGuid();
        await using (var db = NuovoContesto())
        {
            db.Aziende.Add(new AziendaEntity
            {
                Id = aziendaId, TenantId = tenant, RagioneSociale = "Azienda Prova Srl",
                PartitaIva = "01876543214", Indirizzo = "Via Test 2", Cap = "20100",
                Comune = "Milano", Provincia = "MI", Settore = "AGRICOLTURA", CodiceAteco = "01.46.00",
            });
            await db.SaveChangesAsync();
        }

        await using (var db = NuovoContesto())
        {
            var svc = new UniEmensService(db);
            var errore = await Assert.ThrowsAsync<ArgumentException>(() =>
                svc.GeneraDelMeseAsync(tenant, aziendaId, 2026, 6, "000000"));
            Assert.Contains("dati contributivi provvisori", errore.Message);
        }
    }

    private static RapportoLavoroEntity Rapporto(Guid id, Guid tenant, Guid aziendaId, Guid dipendenteId) => new()
    {
        Id = id, TenantId = tenant, AziendaId = aziendaId, DipendenteId = dipendenteId,
        CodiceCcnl = "COMMERCIO_TERZIARIO", Livello = "3", Qualifica = "Impiegato",
        Regione = "12", CodiceCatastaleComune = "F205", DataAssunzione = new DateOnly(2020, 1, 1),
    };

    private static CedolinoEntity Cedolino(
        Guid tenant, Guid aziendaId, Guid rapportoId,
        decimal irpef, decimal addReg, decimal addCom, decimal inpsDip, decimal inpsDat) => new()
    {
        Id = Guid.NewGuid(), TenantId = tenant, AziendaId = aziendaId, RapportoLavoroId = rapportoId,
        Anno = 2026, Mese = 6,
        IrpefNetta = irpef, AddizionaleRegionale = addReg, AddizionaleComunale = addCom,
        ContributiDipendente = inpsDip, ContributiDatore = inpsDat, CalcolatoIl = DateTime.UtcNow,
        Stato = nameof(StatoCedolino.Bozza), Versione = 1,
    };

    public void Dispose() => _connection.Dispose();
}
