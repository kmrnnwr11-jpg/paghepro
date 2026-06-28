using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Paghe.Core.Domain;
using Paghe.Persistence;
using Paghe.Persistence.Entities;
using Xunit;

namespace Paghe.Tests;

/// <summary>
/// Test GDPR — diritto all'oblio (art. 17): l'anonimizzazione di un dipendente cancella i dati
/// personali ma <b>conserva</b> i cedolini (obbligo di conservazione fiscale). Idempotente e
/// isolata per tenant.
/// </summary>
public sealed class GdprDirittoOblioTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public GdprDirittoOblioTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        using var db = NuovoContesto();
        db.Database.EnsureCreated();
        new ParametriNormativiService(db).ImportaSeedSeVuotoAsync().GetAwaiter().GetResult();
    }

    private PagheDbContext NuovoContesto() =>
        new(new DbContextOptionsBuilder<PagheDbContext>().UseSqlite(_connection).Options);

    private async Task<(Guid tenant, Guid dipendenteId, Guid rapportoId)> PreparaAsync()
    {
        var tenant = Guid.NewGuid();
        await using var db = NuovoContesto();
        var az = await new AziendeService(db).CreaAsync(tenant, new AziendaEntity
        {
            RagioneSociale = "Alfa Srl", PartitaIva = "01234567890",
            Indirizzo = "Via Roma 1", Cap = "20100", Comune = "Milano", Provincia = "MI",
        });
        var rapporto = await new AnagraficheLavoroService(db).CreaDipendenteERapportoAsync(
            tenant, az.Id,
            new DipendenteEntity
            {
                CodiceFiscale = "RSSMRA80A01F205X", Nome = "Mario", Cognome = "Rossi",
                Iban = "IT60X0542811101000000123456",
            },
            new RapportoLavoroEntity
            {
                CodiceCcnl = "COMMERCIO_TERZIARIO", Livello = "3", Qualifica = "Impiegato",
                NumeroMensilita = 14, Regione = "Lombardia", CodiceCatastaleComune = "F205",
                DataAssunzione = new DateOnly(2020, 1, 1),
            });
        return (tenant, rapporto.DipendenteId, rapporto.Id);
    }

    [Fact]
    public async Task Anonimizza_CancellaDatiPersonali_MaConservaIlCedolino()
    {
        var (tenant, dipendenteId, rapportoId) = await PreparaAsync();

        await using (var db = NuovoContesto())
            await new CedoliniService(db, new ParametriNormativiService(db))
                .CalcolaEArchiviaAsync(tenant, rapportoId, new Cartellino { Anno = 2026, Mese = 6 });

        await using (var db = NuovoContesto())
            Assert.True(await new AnagraficheLavoroService(db).AnonimizzaDipendenteAsync(tenant, dipendenteId));

        await using (var db = NuovoContesto())
        {
            var dip = await db.Dipendenti.FirstAsync(d => d.Id == dipendenteId);
            Assert.True(dip.Anonimizzato);
            Assert.Equal("(anonimizzato)", dip.Nome);
            Assert.Equal("(anonimizzato)", dip.Cognome);
            Assert.StartsWith("ANON-", dip.CodiceFiscale);
            Assert.Null(dip.Iban);

            // Il cedolino (record contabile) è conservato.
            Assert.Equal(1, await db.Cedolini.CountAsync(c => c.RapportoLavoroId == rapportoId));
        }
    }

    [Fact]
    public async Task Anonimizza_Idempotente_NonRicambiaIDati()
    {
        var (tenant, dipendenteId, _) = await PreparaAsync();

        string cfDopoPrima;
        await using (var db = NuovoContesto())
        {
            await new AnagraficheLavoroService(db).AnonimizzaDipendenteAsync(tenant, dipendenteId);
            cfDopoPrima = (await db.Dipendenti.FirstAsync(d => d.Id == dipendenteId)).CodiceFiscale;
        }
        await using (var db = NuovoContesto())
        {
            Assert.True(await new AnagraficheLavoroService(db).AnonimizzaDipendenteAsync(tenant, dipendenteId));
            var cf = (await db.Dipendenti.FirstAsync(d => d.Id == dipendenteId)).CodiceFiscale;
            Assert.Equal(cfDopoPrima, cf); // la seconda chiamata non rigenera il token
        }
    }

    [Fact]
    public async Task Anonimizza_TenantDiverso_NonTrova()
    {
        var (_, dipendenteId, _) = await PreparaAsync();
        await using var db = NuovoContesto();
        Assert.False(await new AnagraficheLavoroService(db)
            .AnonimizzaDipendenteAsync(Guid.NewGuid(), dipendenteId));
    }

    public void Dispose() => _connection.Dispose();
}
