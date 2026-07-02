using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Paghe.Persistence;
using Paghe.Persistence.Entities;
using Xunit;

namespace Paghe.Tests;

/// <summary>
/// Correzione della scheda dipendente/rapporto (es. dopo un import UNILAV/OCR letto male):
/// aggiorna solo i campi indicati, è isolata per tenant e rifiuta i dipendenti anonimizzati (GDPR).
/// </summary>
public sealed class CorreggiRapportoTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly Guid _tenant = Guid.NewGuid();

    public CorreggiRapportoTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        using var db = NuovoContesto();
        db.Database.EnsureCreated();
    }

    private PagheDbContext NuovoContesto() =>
        new(new DbContextOptionsBuilder<PagheDbContext>().UseSqlite(_connection).Options);

    private async Task<Guid> CreaRapportoAsync()
    {
        await using var db = NuovoContesto();
        var azienda = await new AziendeService(db).CreaAsync(_tenant, new AziendaEntity
        {
            RagioneSociale = "Alfa Srl", PartitaIva = "01234567890",
            Indirizzo = "Via Roma 1", Cap = "20100", Comune = "Milano", Provincia = "MI",
        });
        var rapporto = await new AnagraficheLavoroService(db).CreaDipendenteERapportoAsync(
            _tenant, azienda.Id,
            // Dati "letti male" dall'OCR, da correggere.
            new DipendenteEntity { CodiceFiscale = "RSSMRA80A01F205X", Nome = "Marlo", Cognome = "Rossl" },
            new RapportoLavoroEntity
            {
                CodiceCcnl = "COMMERCIO_TERZIARIO", Livello = "4", Qualifica = "Impiegato",
                NumeroMensilita = 14, Regione = "Lombardia", CodiceCatastaleComune = "F205",
                DataAssunzione = new DateOnly(2024, 1, 1),
            });
        return rapporto.Id;
    }

    [Fact]
    public async Task Correggi_CambiaSoloICampiIndicati()
    {
        var rapportoId = await CreaRapportoAsync();

        await using (var db = NuovoContesto())
        {
            var r = await new AnagraficheLavoroService(db).CorreggiRapportoAsync(_tenant, rapportoId,
                new AnagraficheLavoroService.CorrezioneRapporto(
                    Nome: "Mario", Cognome: "Rossi", Livello: "3",
                    DataAssunzione: new DateOnly(2024, 2, 1)));
            Assert.NotNull(r);
        }

        await using (var db2 = NuovoContesto())
        {
            var r = await new AnagraficheLavoroService(db2).GetRapportoAsync(_tenant, rapportoId);
            Assert.Equal("Mario", r!.Dipendente!.Nome);
            Assert.Equal("Rossi", r.Dipendente.Cognome);
            Assert.Equal("3", r.Livello);
            Assert.Equal(new DateOnly(2024, 2, 1), r.DataAssunzione);
            // Non indicati: invariati.
            Assert.Equal("RSSMRA80A01F205X", r.Dipendente.CodiceFiscale);
            Assert.Equal("COMMERCIO_TERZIARIO", r.CodiceCcnl);
            Assert.Equal(14, r.NumeroMensilita);
        }
    }

    [Fact]
    public async Task Correggi_TenantDiverso_NonTrova()
    {
        var rapportoId = await CreaRapportoAsync();
        await using var db = NuovoContesto();
        var r = await new AnagraficheLavoroService(db).CorreggiRapportoAsync(Guid.NewGuid(), rapportoId,
            new AnagraficheLavoroService.CorrezioneRapporto(Nome: "Intruso"));
        Assert.Null(r);
    }

    [Fact]
    public async Task Correggi_DipendenteAnonimizzato_Rifiuta()
    {
        var rapportoId = await CreaRapportoAsync();
        await using var db = NuovoContesto();
        var svc = new AnagraficheLavoroService(db);
        var rapporto = await svc.GetRapportoAsync(_tenant, rapportoId);
        Assert.True(await svc.AnonimizzaDipendenteAsync(_tenant, rapporto!.DipendenteId));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.CorreggiRapportoAsync(_tenant, rapportoId,
                new AnagraficheLavoroService.CorrezioneRapporto(Nome: "Mario")));
    }

    public void Dispose() => _connection.Dispose();
}
