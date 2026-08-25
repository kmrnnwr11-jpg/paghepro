using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Paghe.Core.Domain;
using Paghe.Persistence;
using Paghe.Persistence.Entities;
using Xunit;

namespace Paghe.Tests;

/// <summary>Orologio fermo su una data: per testare regole che dipendono dal "oggi".</summary>
internal sealed class OrologioFisso(DateTimeOffset ora) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => ora;
}

/// <summary>
/// Regola di chiusura periodi: la busta paga si archivia al massimo per il mese corrente;
/// i mesi futuri sono bloccati con un messaggio chiaro. I mesi passati restano elaborabili.
/// </summary>
public sealed class BloccoPeriodiFuturiTests : IDisposable
{
    private static readonly TimeProvider Oggi15Luglio2026 =
        new OrologioFisso(new DateTimeOffset(2026, 7, 15, 12, 0, 0, TimeSpan.Zero));

    private readonly SqliteConnection _connection;

    public BloccoPeriodiFuturiTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        using var db = NuovoContesto();
        db.Database.EnsureCreated();
        new ParametriNormativiService(db).ImportaSeedSeVuotoAsync().GetAwaiter().GetResult();
    }

    private PagheDbContext NuovoContesto() =>
        new(new DbContextOptionsBuilder<PagheDbContext>().UseSqlite(_connection).Options);

    private async Task<Guid> SeedAziendaAsync(Guid tenant, string nome = "Alfa Srl", string piva = "01234567897")
    {
        await using var db = NuovoContesto();
        var azienda = await new AziendeService(db).CreaAsync(tenant, new AziendaEntity
        {
            RagioneSociale = nome, PartitaIva = piva,
            Indirizzo = "Via Roma 1", Cap = "20100", Comune = "Milano", Provincia = "MI",
        });
        return azienda.Id;
    }

    private async Task<Guid> SeedRapportoAsync(
        Guid tenant, Guid? aziendaId = null, DateOnly? assunzione = null)
    {
        aziendaId ??= await SeedAziendaAsync(tenant);
        await using var db = NuovoContesto();
        var rapporto = await new AnagraficheLavoroService(db).CreaDipendenteERapportoAsync(
            tenant, aziendaId.Value,
            new DipendenteEntity { CodiceFiscale = "RSSMRA80A01F205X", Nome = "Mario", Cognome = "Rossi" },
            new RapportoLavoroEntity
            {
                CodiceCcnl = "COMMERCIO_TERZIARIO", Livello = "3", Qualifica = "Impiegato",
                Regione = "Lombardia", CodiceCatastaleComune = "F205",
                DataAssunzione = assunzione ?? new DateOnly(2024, 1, 1),
            });
        return rapporto.Id;
    }

    [Theory]
    [InlineData(2026, 7)] // mese corrente: ammesso
    [InlineData(2026, 6)] // mese passato: ammesso
    [InlineData(2026, 1)] // inizio anno: ammesso
    public async Task MeseCorrenteOPassato_SiElabora(int anno, int mese)
    {
        var tenant = Guid.NewGuid();
        var rapportoId = await SeedRapportoAsync(tenant);

        await using var db = NuovoContesto();
        var svc = new CedoliniService(db, new ParametriNormativiService(db), Oggi15Luglio2026);
        var cedolino = await svc.CalcolaEArchiviaAsync(
            tenant, rapportoId, new Cartellino { Anno = anno, Mese = mese });
        Assert.True(cedolino.NettoInBusta > 0m);
    }

    [Theory]
    [InlineData(2026, 8)]  // mese prossimo
    [InlineData(2026, 12)] // dicembre futuro
    [InlineData(2027, 1)]  // anno successivo
    public async Task MeseFuturo_Bloccato(int anno, int mese)
    {
        var tenant = Guid.NewGuid();
        var rapportoId = await SeedRapportoAsync(tenant);

        await using var db = NuovoContesto();
        var svc = new CedoliniService(db, new ParametriNormativiService(db), Oggi15Luglio2026);
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            svc.CalcolaEArchiviaAsync(tenant, rapportoId, new Cartellino { Anno = anno, Mese = mese }));
        Assert.Contains("futuro", ex.Message);
        Assert.Contains("07/2026", ex.Message); // il messaggio dice qual è il mese corrente

        // Il blocco è avvenuto prima dell'archiviazione: nessun cedolino salvato.
        await using var verifica = NuovoContesto();
        Assert.Empty(verifica.Cedolini.Where(c => c.TenantId == tenant));
    }

    [Fact]
    public async Task MesePrimaDellAssunzione_Bloccato()
    {
        var tenant = Guid.NewGuid();
        var rapportoId = await SeedRapportoAsync(tenant, assunzione: new DateOnly(2026, 3, 15));

        await using var db = NuovoContesto();
        var svc = new CedoliniService(db, new ParametriNormativiService(db), Oggi15Luglio2026);

        // Febbraio precede l'assunzione (15/03/2026): bloccato con messaggio chiaro.
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            svc.CalcolaEArchiviaAsync(tenant, rapportoId, new Cartellino { Anno = 2026, Mese = 2 }));
        Assert.Contains("precede la data di assunzione", ex.Message);
        Assert.Contains("15/03/2026", ex.Message);

        // Il mese dell'assunzione invece si elabora.
        var marzo = await svc.CalcolaEArchiviaAsync(
            tenant, rapportoId, new Cartellino { Anno = 2026, Mese = 3 });
        Assert.True(marzo.NettoInBusta > 0m);
    }

    [Fact]
    public async Task StessoCodiceFiscale_NonAssumibileDueVolte_NellaStessaAzienda()
    {
        var tenant = Guid.NewGuid();
        var aziendaId = await SeedAziendaAsync(tenant);
        await SeedRapportoAsync(tenant, aziendaId);

        // Stessa persona, stessa azienda (CF minuscolo: la regola normalizza): bloccato.
        await using var db = NuovoContesto();
        var svc = new AnagraficheLavoroService(db);
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => svc.CreaDipendenteERapportoAsync(
            tenant, aziendaId,
            new DipendenteEntity { CodiceFiscale = "rssmra80a01f205x", Nome = "Mario", Cognome = "Rossi" },
            new RapportoLavoroEntity
            {
                CodiceCcnl = "COMMERCIO_TERZIARIO", Livello = "3", Qualifica = "Impiegato",
                Regione = "Lombardia", CodiceCatastaleComune = "F205",
            }));
        Assert.Contains("già assunto", ex.Message);
    }

    [Fact]
    public async Task MeseDopoLaCessazione_Bloccato_EMeseDellaCessazioneAmmesso()
    {
        var tenant = Guid.NewGuid();
        var rapportoId = await SeedRapportoAsync(tenant);

        // Il rapporto cessa il 20/05/2026.
        await using (var db = NuovoContesto())
        {
            var r = await new AnagraficheLavoroService(db).CorreggiRapportoAsync(tenant, rapportoId,
                new AnagraficheLavoroService.CorrezioneRapporto(
                    DataCessazione: new DateOnly(2026, 5, 20)));
            Assert.Equal(new DateOnly(2026, 5, 20), r!.DataCessazione);
        }

        await using var db2 = NuovoContesto();
        var svc = new CedoliniService(db2, new ParametriNormativiService(db2), Oggi15Luglio2026);

        // Giugno è interamente dopo la cessazione: bloccato.
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            svc.CalcolaEArchiviaAsync(tenant, rapportoId, new Cartellino { Anno = 2026, Mese = 6 }));
        Assert.Contains("successivo alla cessazione", ex.Message);
        Assert.Contains("20/05/2026", ex.Message);

        // Il mese della cessazione (maggio) si elabora ancora.
        var maggio = await svc.CalcolaEArchiviaAsync(
            tenant, rapportoId, new Cartellino { Anno = 2026, Mese = 5 });
        Assert.True(maggio.NettoInBusta > 0m);
    }

    [Fact]
    public async Task CessazionePrimaDellAssunzione_Rifiutata_ERimozioneCessazione()
    {
        var tenant = Guid.NewGuid();
        var rapportoId = await SeedRapportoAsync(tenant, assunzione: new DateOnly(2024, 6, 1));

        await using var db = NuovoContesto();
        var svc = new AnagraficheLavoroService(db);
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => svc.CorreggiRapportoAsync(
            tenant, rapportoId,
            new AnagraficheLavoroService.CorrezioneRapporto(DataCessazione: new DateOnly(2024, 1, 1))));
        Assert.Contains("non può precedere", ex.Message);

        // Imposta e poi rimuovi (errore corretto): il rapporto torna in forza.
        await svc.CorreggiRapportoAsync(tenant, rapportoId,
            new AnagraficheLavoroService.CorrezioneRapporto(DataCessazione: new DateOnly(2026, 5, 20)));
        var r = await svc.CorreggiRapportoAsync(tenant, rapportoId,
            new AnagraficheLavoroService.CorrezioneRapporto(RimuoviDataCessazione: true));
        Assert.Null(r!.DataCessazione);
    }

    [Fact]
    public async Task CertificazioneUnica_RiportaLaCessazione_ENonInForza()
    {
        var tenant = Guid.NewGuid();
        var rapportoId = await SeedRapportoAsync(tenant);

        await using (var db = NuovoContesto())
        {
            var svc = new CedoliniService(db, new ParametriNormativiService(db), Oggi15Luglio2026);
            var cedolino = await svc.CalcolaEArchiviaAsync(
                tenant, rapportoId, new Cartellino { Anno = 2026, Mese = 5 });
            await db.ClassificaPreWorkflowAsync(tenant, cedolino.Id);
            await new AnagraficheLavoroService(db).CorreggiRapportoAsync(tenant, rapportoId,
                new AnagraficheLavoroService.CorrezioneRapporto(
                    DataCessazione: new DateOnly(2026, 5, 20)));
        }

        await using var db2 = NuovoContesto();
        var cu = await new CertificazioneUnicaService(db2).CostruisciModelloAsync(tenant, rapportoId, 2026);
        Assert.Equal(new DateOnly(2026, 5, 20), cu!.DatiFiscali.DataCessazione);
        Assert.False(cu.DatiFiscali.InForzaAl31Dicembre);
    }

    [Fact]
    public async Task MeseConF24Inviato_Bloccato_MaRicalcolabileConConfermaEsplicita()
    {
        var tenant = Guid.NewGuid();
        var aziendaId = await SeedAziendaAsync(tenant);
        var rapportoId = await SeedRapportoAsync(tenant, aziendaId);

        await using (var db = NuovoContesto())
        {
            var svc = new CedoliniService(db, new ParametriNormativiService(db), Oggi15Luglio2026);
            await svc.CalcolaEArchiviaAsync(tenant, rapportoId, new Cartellino { Anno = 2026, Mese = 6 });

            // F24 del mese inviato (stato oltre la bozza).
            db.PraticheF24.Add(new PraticaF24Entity
            {
                Id = Guid.NewGuid(), TenantId = tenant, AziendaId = aziendaId,
                Anno = 2026, Mese = 6, Stato = "InviataManualmente",
                CreataIl = DateTime.UtcNow, UltimoAggiornamento = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        await using var db2 = NuovoContesto();
        var svc2 = new CedoliniService(db2, new ParametriNormativiService(db2), Oggi15Luglio2026);

        // Ricalcolo normale: bloccato con messaggio che spiega la via d'uscita.
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => svc2.CalcolaEArchiviaAsync(
            tenant, rapportoId, new Cartellino { Anno = 2026, Mese = 6, OreStraordinario = 2m }));
        Assert.Contains("F24 già inviato", ex.Message);
        Assert.Contains("ricalcolo forzato", ex.Message);

        // Il commercialista conferma (errore da correggere): il ricalcolo passa.
        var corretto = await svc2.CalcolaEArchiviaAsync(
            tenant, rapportoId, new Cartellino { Anno = 2026, Mese = 6, OreStraordinario = 2m },
            forzaRicalcolo: true);
        Assert.True(corretto.NettoInBusta > 0m);

        // Un altro mese della stessa azienda non è toccato dal blocco.
        var luglio = await svc2.CalcolaEArchiviaAsync(
            tenant, rapportoId, new Cartellino { Anno = 2026, Mese = 7 });
        Assert.True(luglio.NettoInBusta > 0m);
    }

    [Fact]
    public async Task StessoCodiceFiscale_AmmessoInUnAziendaDiversa()
    {
        var tenant = Guid.NewGuid();
        var aziendaA = await SeedAziendaAsync(tenant);
        await SeedRapportoAsync(tenant, aziendaA);

        // Stessa persona ma altra azienda dello studio: ammesso (es. due part-time).
        var aziendaB = await SeedAziendaAsync(tenant, "Beta Srl", "09876543217");
        var rapportoB = await SeedRapportoAsync(tenant, aziendaB);

        // Ogni busta paga resta agganciata all'azienda del proprio rapporto di assunzione.
        await using var db = NuovoContesto();
        var cedolino = await new CedoliniService(db, new ParametriNormativiService(db), Oggi15Luglio2026)
            .CalcolaEArchiviaAsync(tenant, rapportoB, new Cartellino { Anno = 2026, Mese = 6 });
        Assert.Equal(aziendaB, cedolino.AziendaId);
    }

    public void Dispose() => _connection.Dispose();
}
