using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Paghe.Core.Domain;
using Paghe.Persistence;
using Paghe.Persistence.Entities;
using Xunit;

namespace Paghe.Tests;

/// <summary>
/// Test di integrazione del flusso lavoro su SQLite: creazione dipendente/rapporto,
/// calcolo e archiviazione del cedolino, rilettura, ricalcolo idempotente e isolamento tenant.
/// </summary>
public sealed class PersistenzaCedoliniTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public PersistenzaCedoliniTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        using var db = NuovoContesto();
        db.Database.EnsureCreated();
        new ParametriNormativiService(db).ImportaSeedSeVuotoAsync().GetAwaiter().GetResult();
    }

    private PagheDbContext NuovoContesto() =>
        new(new DbContextOptionsBuilder<PagheDbContext>().UseSqlite(_connection).Options);

    private async Task<(Guid tenant, Guid aziendaId)> CreaAziendaAsync()
    {
        var tenant = Guid.NewGuid();
        await using var db = NuovoContesto();
        var az = await new AziendeService(db).CreaAsync(tenant, new AziendaEntity
        {
            RagioneSociale = "Alfa Srl", PartitaIva = "01234567890",
            Indirizzo = "Via Roma 1", Cap = "20100", Comune = "Milano", Provincia = "MI",
        });
        return (tenant, az.Id);
    }

    private async Task<Guid> CreaRapportoAsync(Guid tenant, Guid aziendaId)
    {
        await using var db = NuovoContesto();
        var rapporto = await new AnagraficheLavoroService(db).CreaDipendenteERapportoAsync(
            tenant, aziendaId,
            new DipendenteEntity
            {
                CodiceFiscale = "RSSMRA80A01F205X", Nome = "Mario", Cognome = "Rossi",
                Iban = "IT60X0542811101000000123456",
            },
            new RapportoLavoroEntity
            {
                CodiceCcnl = "COMMERCIO_TERZIARIO", Livello = "3", Qualifica = "Impiegato",
                NumeroMensilita = 14, Regione = "Lombardia", CodiceCatastaleComune = "F205",
                DataAssunzione = new DateOnly(2020, 1, 1), TipoContratto = "Tempo indeterminato",
            });
        return rapporto.Id;
    }

    [Fact]
    public async Task CalcolaEArchivia_PoiRileggi_ConVociENetto()
    {
        var (tenant, aziendaId) = await CreaAziendaAsync();
        var rapportoId = await CreaRapportoAsync(tenant, aziendaId);

        Guid cedolinoId;
        decimal nettoCalcolato;
        await using (var db = NuovoContesto())
        {
            var svc = new CedoliniService(db, new ParametriNormativiService(db));
            var c = await svc.CalcolaEArchiviaAsync(tenant, rapportoId,
                new Cartellino { Anno = 2026, Mese = 6 });
            cedolinoId = c.Id;
            nettoCalcolato = c.NettoInBusta;
            Assert.Equal(1340.99m, c.TotaleCompetenze); // le competenze non dipendono dalle addizionali
            Assert.True(c.NettoInBusta > 0m && c.NettoInBusta < c.TotaleCompetenze);
        }

        await using (var db = NuovoContesto())
        {
            var c = await new CedoliniService(db, new ParametriNormativiService(db)).GetAsync(tenant, cedolinoId);
            Assert.NotNull(c);
            Assert.Equal(2026, c!.Anno);
            Assert.Equal(nettoCalcolato, c.NettoInBusta); // rileggendo: stesso netto calcolato
            Assert.NotEmpty(c.Voci); // le righe del cedolino sono persistite
        }
    }

    [Fact]
    public async Task Ricalcolo_StessoPeriodo_NonDuplica()
    {
        var (tenant, aziendaId) = await CreaAziendaAsync();
        var rapportoId = await CreaRapportoAsync(tenant, aziendaId);

        await using (var db = NuovoContesto())
        {
            var svc = new CedoliniService(db, new ParametriNormativiService(db));
            await svc.CalcolaEArchiviaAsync(tenant, rapportoId, new Cartellino { Anno = 2026, Mese = 6 });
            await svc.CalcolaEArchiviaAsync(tenant, rapportoId, new Cartellino { Anno = 2026, Mese = 6 });
        }

        await using (var db = NuovoContesto())
        {
            var elenco = await new CedoliniService(db, new ParametriNormativiService(db))
                .ElencoPerRapportoAsync(tenant, rapportoId);
            Assert.Single(elenco); // il ricalcolo sostituisce, non duplica
        }
    }

    [Fact]
    public async Task Calcola_RapportoInesistente_LanciaEccezione()
    {
        var tenant = Guid.NewGuid();
        await using var db = NuovoContesto();
        var svc = new CedoliniService(db, new ParametriNormativiService(db));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            svc.CalcolaEArchiviaAsync(tenant, Guid.NewGuid(), new Cartellino { Anno = 2026, Mese = 6 }));
    }

    [Fact]
    public async Task Calcola_CartellinoNonValido_NonVieneArchiviato()
    {
        var (tenant, aziendaId) = await CreaAziendaAsync();
        var rapportoId = await CreaRapportoAsync(tenant, aziendaId);

        await using var db = NuovoContesto();
        var svc = new CedoliniService(db, new ParametriNormativiService(db));
        var errore = await Assert.ThrowsAsync<ArgumentException>(() =>
            svc.CalcolaEArchiviaAsync(tenant, rapportoId, new Cartellino
            {
                Anno = 2026,
                Mese = 2,
                GiorniFerie = 20m,
                GiorniMalattia = 10m,
            }));

        Assert.Contains("il mese ne contiene 28", errore.Message);
        Assert.Empty(await db.Cartellini.Where(c => c.TenantId == tenant).ToListAsync());
        Assert.Empty(await db.Cedolini.Where(c => c.TenantId == tenant).ToListAsync());
    }

    [Fact]
    public async Task RicalcolaPerPdf_DopoArchiviazione_RestituisceDominioENomi()
    {
        var (tenant, aziendaId) = await CreaAziendaAsync();
        var rapportoId = await CreaRapportoAsync(tenant, aziendaId);

        Guid cedolinoId;
        decimal nettoArchiviato;
        await using (var db = NuovoContesto())
        {
            var svc = new CedoliniService(db, new ParametriNormativiService(db));
            var c = await svc.CalcolaEArchiviaAsync(tenant, rapportoId, new Cartellino { Anno = 2026, Mese = 6 });
            cedolinoId = c.Id;
            nettoArchiviato = c.NettoInBusta;
        }

        await using (var db = NuovoContesto())
        {
            var dati = await new CedoliniService(db, new ParametriNormativiService(db))
                .LeggiPerPdfAsync(tenant, cedolinoId);
            Assert.NotNull(dati);
            Assert.Equal("Alfa Srl", dati!.Intestazione.Azienda);
            Assert.Equal("Rossi Mario", dati.Intestazione.Dipendente);
            Assert.Equal("IT60X0542811101000000123456", dati.Intestazione.IbanDipendente);
            Assert.Equal("Tempo indeterminato", dati.Intestazione.TipoContratto);
            Assert.Equal(6, dati.Cedolino.Mese);
            Assert.Equal(2026, dati.Cedolino.Anno);
            Assert.NotEmpty(dati.Cedolino.Righe);          // le voci per il PDF ci sono
            Assert.Equal(nettoArchiviato, dati.Cedolino.NettoInBusta); // ricalcolo == archiviato
            Assert.Equal(6, dati.Cartellino.Mese);
        }
    }

    [Fact]
    public async Task RicalcolaPerPdf_TenantDiverso_RestituisceNull()
    {
        var (tenant, aziendaId) = await CreaAziendaAsync();
        var rapportoId = await CreaRapportoAsync(tenant, aziendaId);

        Guid cedolinoId;
        await using (var db = NuovoContesto())
        {
            var svc = new CedoliniService(db, new ParametriNormativiService(db));
            cedolinoId = (await svc.CalcolaEArchiviaAsync(tenant, rapportoId, new Cartellino { Anno = 2026, Mese = 6 })).Id;
        }

        await using (var db = NuovoContesto())
        {
            var dati = await new CedoliniService(db, new ParametriNormativiService(db))
                .LeggiPerPdfAsync(Guid.NewGuid(), cedolinoId); // tenant estraneo
            Assert.Null(dati);
        }
    }

    [Fact]
    public async Task ElementiAgevolati_SopravvivonoAllArchiviazioneEAlRicalcoloPerIlPdf()
    {
        // Senza persistenza degli elementi agevolati il PDF, che si rigenera ricalcolando dal
        // cartellino archiviato, mostrerebbe una busta senza trasferta né buoni pasto e con
        // totali diversi da quelli archiviati.
        var (tenant, aziendaId) = await CreaAziendaAsync();
        var rapportoId = await CreaRapportoAsync(tenant, aziendaId);

        Guid cedolinoId;
        decimal nettoArchiviato;
        await using (var db = NuovoContesto())
        {
            var c = await new CedoliniService(db, new ParametriNormativiService(db))
                .CalcolaEArchiviaAsync(tenant, rapportoId, new Cartellino
                {
                    Anno = 2026, Mese = 6,
                    GiorniTrasfertaItalia = 5m,
                    IndennitaTrasfertaGiornaliera = 60m,   // 13,52/giorno oltre soglia
                    RimborsoTrasferta = TipoRimborsoTrasferta.MistoVittoOAlloggio,
                    GiorniBuonoPasto = 20, ValoreBuonoPasto = 10m,
                    FringeBenefit = 200m, FigliACarico = true,
                    PremioRisultato = 1000m, RedditoAnnoPrecedente = 25000m,
                });
            cedolinoId = c.Id;
            nettoArchiviato = c.NettoInBusta;
        }

        await using (var db = NuovoContesto())
        {
            var dati = await new CedoliniService(db, new ParametriNormativiService(db))
                .LeggiPerPdfAsync(tenant, cedolinoId);

            Assert.NotNull(dati);
            // Il netto ricalcolato per il PDF deve coincidere con quello archiviato.
            Assert.Equal(nettoArchiviato, dati!.Cedolino.NettoInBusta);

            // Le voci devono esserci davvero, non solo i totali.
            var codici = dati.Cedolino.Righe.Select(r => r.Codice).ToList();
            Assert.Contains("TRASF_IT", codici);
            Assert.Contains("TRASF_IT_IMP", codici);      // eccedenza con soglia ridotta di 1/3
            Assert.Contains("BUONI_PASTO", codici);
            Assert.Contains("FRINGE", codici);
            Assert.Contains("PREMIO", codici);
            Assert.Contains("PREMIO_IMP", codici);

            Assert.True(dati.Cedolino.TotaleEsenti > 0m);
            Assert.Equal(1000m, dati.Cedolino.PremioRisultatoAgevolato);
        }
    }

    [Fact]
    public async Task AssegniFamiliariSpettanti_SonoPersistitiERestanoFuoriDaImponibiliECosto()
    {
        var (tenant, aziendaId) = await CreaAziendaAsync();
        var rapportoId = await CreaRapportoAsync(tenant, aziendaId);

        Guid cedolinoId;
        decimal imponibileInps;
        decimal imponibileIrpef;
        decimal costoAzienda;
        await using (var db = NuovoContesto())
        {
            var servizio = new CedoliniService(db, new ParametriNormativiService(db));
            var senzaAnf = await servizio.CalcolaEArchiviaAsync(
                tenant, rapportoId, new Cartellino { Anno = 2026, Mese = 5 });
            imponibileInps = senzaAnf.ImponibileInps;
            imponibileIrpef = senzaAnf.ImponibileIrpef;
            costoAzienda = senzaAnf.CostoAzienda;

            var conAnf = await servizio.CalcolaEArchiviaAsync(tenant, rapportoId, new Cartellino
            {
                Anno = 2026,
                Mese = 6,
                StatoAssegniFamiliari = StatoAssegniFamiliari.Spettanti,
                ImportoAssegniFamiliari = 123.45m,
            });
            cedolinoId = conAnf.Id;

            var rigaAnf = Assert.Single(conAnf.Voci, v => v.Codice == "ANF");
            Assert.Equal(123.45m, rigaAnf.Importo);
            Assert.Equal(imponibileInps, conAnf.ImponibileInps);
            Assert.Equal(imponibileIrpef, conAnf.ImponibileIrpef);
            Assert.Equal(costoAzienda, conAnf.CostoAzienda);
            Assert.Equal(123.45m, conAnf.NettoInBusta - senzaAnf.NettoInBusta);
        }

        await using (var db = NuovoContesto())
        {
            var cartellino = await db.Cartellini.SingleAsync(c =>
                c.TenantId == tenant && c.RapportoLavoroId == rapportoId
                    && c.Anno == 2026 && c.Mese == 6);
            Assert.Equal(nameof(StatoAssegniFamiliari.Spettanti), cartellino.StatoAssegniFamiliari);
            Assert.Equal(123.45m, cartellino.ImportoAssegniFamiliari);

            var datiPdf = await new CedoliniService(db, new ParametriNormativiService(db))
                .LeggiPerPdfAsync(tenant, cedolinoId);
            Assert.NotNull(datiPdf);
            Assert.Equal(StatoAssegniFamiliari.Spettanti, datiPdf!.Cartellino.StatoAssegniFamiliari);
            Assert.Equal(123.45m, datiPdf.Cartellino.ImportoAssegniFamiliari);
            Assert.Contains(datiPdf.Cedolino.Righe, r => r.Codice == "ANF" && r.Importo == 123.45m);
        }
    }

    [Fact]
    public async Task ResiduiInizialiESaldiDeiMesiContabilizzati_SiCumulanoSenzaUsareBozze()
    {
        var (tenant, aziendaId) = await CreaAziendaAsync();
        var rapportoId = await CreaRapportoAsync(tenant, aziendaId);

        await using (var db = NuovoContesto())
        {
            var rapporto = await db.Rapporti.SingleAsync(r =>
                r.TenantId == tenant && r.Id == rapportoId);
            rapporto.FerieResidueIniziali = 10m;
            rapporto.PermessiResiduiIniziali = 20m;
            await db.SaveChangesAsync();

            var servizio = new CedoliniService(db, new ParametriNormativiService(db));
            var gennaio = await servizio.CalcolaEArchiviaAsync(tenant, rapportoId, new Cartellino
            {
                Anno = 2026,
                Mese = 1,
                GiorniFerie = 2m,
                OrePermessi = 3m,
            });

            Assert.Equal(10m + gennaio.FerieMaturate - 2m, gennaio.FerieResidue);
            Assert.Equal(20m + gennaio.PermessiMaturati - 3m, gennaio.PermessiResidui);

            // Fixture downstream: gennaio rappresenta un mese già contabilizzato. La macchina a
            // stati completa è verificata separatamente in CedolinoWorkflowTests.
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE Cedolini SET Stato = {nameof(StatoCedolino.Chiusa)} WHERE Id = {gennaio.Id} AND TenantId = {tenant}");

            var febbraio = await servizio.CalcolaEArchiviaAsync(tenant, rapportoId, new Cartellino
            {
                Anno = 2026,
                Mese = 2,
                GiorniFerie = 1m,
                OrePermessi = 2m,
                OreExFestivita = 1m,
            });

            Assert.Equal(3m, febbraio.FerieGodute);
            Assert.Equal(10m + febbraio.FerieMaturate - 3m, febbraio.FerieResidue);
            Assert.Equal(6m, febbraio.PermessiGoduti);
            Assert.Equal(20m + febbraio.PermessiMaturati - 6m, febbraio.PermessiResidui);

            // Febbraio resta Bozza: marzo non deve usarne le assenze nei progressivi.
            var marzo = await servizio.CalcolaEArchiviaAsync(tenant, rapportoId, new Cartellino
            {
                Anno = 2026,
                Mese = 3,
                GiorniFerie = 1m,
                OrePermessi = 2m,
            });
            Assert.Equal(3m, marzo.FerieGodute); // gennaio 2 + marzo 1; febbraio escluso
            Assert.Equal(10m + marzo.FerieMaturate - 3m, marzo.FerieResidue);
            Assert.Equal(5m, marzo.PermessiGoduti); // gennaio 3 + marzo 2; febbraio escluso
            Assert.Equal(20m + marzo.PermessiMaturati - 5m, marzo.PermessiResidui);
        }
    }

    public void Dispose() => _connection.Dispose();
}
