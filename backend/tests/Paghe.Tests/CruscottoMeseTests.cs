using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Paghe.Core.Domain;
using Paghe.Persistence;
using Paghe.Persistence.Entities;
using Xunit;

namespace Paghe.Tests;

/// <summary>
/// Cruscotto del mese multi-azienda: semafori per rapporto con la stessa tassonomia del
/// broker Aurora, ciclo agricolo dall'archivio dedicato, isolamento tenant.
/// </summary>
public sealed class CruscottoMeseTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly Guid _tenant = Guid.NewGuid();

    public CruscottoMeseTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        using var db = NuovoContesto();
        db.Database.EnsureCreated();
    }

    public void Dispose() => _connection.Dispose();

    private PagheDbContext NuovoContesto() =>
        new(new DbContextOptionsBuilder<PagheDbContext>().UseSqlite(_connection).Options);

    private static AziendaEntity Azienda(Guid tenant, string nome, bool completa) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenant,
        RagioneSociale = nome,
        MatricolaInps = completa ? "1234567890" : null,
        CodiceSedeInps = completa ? "4900" : null,
        PatInail = completa ? "12345678" : null,
        CcnlDefault = completa ? "COMMERCIO_TERZIARIO" : null,
    };

    private static (DipendenteEntity, RapportoLavoroEntity) Rapporto(
        Guid tenant, Guid aziendaId, string cognome,
        string ccnl = "COMMERCIO_TERZIARIO", DateOnly? cessazione = null)
    {
        var dipendente = new DipendenteEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenant,
            CodiceFiscale = $"CF{Guid.NewGuid():N}"[..16].ToUpperInvariant(),
            Nome = "Test",
            Cognome = cognome,
        };
        var rapporto = new RapportoLavoroEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenant,
            AziendaId = aziendaId,
            DipendenteId = dipendente.Id,
            CodiceCcnl = ccnl,
            Livello = "3",
            Qualifica = "Operaio",
            TipoContratto = "Tempo indeterminato",
            DataAssunzione = new DateOnly(2025, 1, 1),
            DataCessazione = cessazione,
        };
        return (dipendente, rapporto);
    }

    [Fact]
    public async Task ClassificaTuttiGliStati_EContaISemafori()
    {
        await using var db = NuovoContesto();
        db.Tenants.Add(new TenantEntity
        {
            Id = _tenant, RagioneSociale = "Studio", CreatoIl = DateTime.UtcNow,
        });
        var alfa = Azienda(_tenant, "Alfa Srl", completa: true);
        var beta = Azienda(_tenant, "Beta Srl", completa: false);
        db.Aziende.AddRange(alfa, beta);

        var (dChiuso, rChiuso) = Rapporto(_tenant, alfa.Id, "Chiuso");
        var (dBozza, rBozza) = Rapporto(_tenant, alfa.Id, "Bozza");
        var (dPronto, rPronto) = Rapporto(_tenant, alfa.Id, "Pronto");
        var (dVuoto, rVuoto) = Rapporto(_tenant, alfa.Id, "Vuoto");
        var (dAgrOk, rAgrOk) = Rapporto(_tenant, alfa.Id, "AgricoloFatto", "AGRICOLTURA_OPERAI");
        var (dAgrNo, rAgrNo) = Rapporto(_tenant, alfa.Id, "AgricoloDaFare", "AGRICOLTURA_OPERAI");
        var (dCessato, rCessato) = Rapporto(
            _tenant, alfa.Id, "Cessato", cessazione: new DateOnly(2026, 5, 31));
        var (dBloccato, rBloccato) = Rapporto(_tenant, beta.Id, "Bloccato");
        db.Dipendenti.AddRange(
            dChiuso, dBozza, dPronto, dVuoto, dAgrOk, dAgrNo, dCessato, dBloccato);
        db.Rapporti.AddRange(
            rChiuso, rBozza, rPronto, rVuoto, rAgrOk, rAgrNo, rCessato, rBloccato);

        // Il workflow impone la nascita in Bozza: il cedolino «Chiuso» viene promosso dopo
        // con un UPDATE diretto, la stessa strada di AiQueryBrokerTests.
        var cedolinoChiusoId = Guid.NewGuid();
        db.Cedolini.Add(new CedolinoEntity
        {
            Id = cedolinoChiusoId, TenantId = _tenant, AziendaId = alfa.Id,
            RapportoLavoroId = rChiuso.Id, Anno = 2026, Mese = 7,
            Stato = nameof(StatoCedolino.Bozza),
        });
        db.Cedolini.Add(new CedolinoEntity
        {
            Id = Guid.NewGuid(), TenantId = _tenant, AziendaId = alfa.Id,
            RapportoLavoroId = rBozza.Id, Anno = 2026, Mese = 7,
            Stato = nameof(StatoCedolino.Bozza),
        });
        db.Cartellini.Add(new CartellinoEntity
        {
            Id = Guid.NewGuid(), TenantId = _tenant,
            RapportoLavoroId = rPronto.Id, Anno = 2026, Mese = 7,
        });
        db.CedoliniAgricoli.Add(new CedolinoAgricoloEntity
        {
            Id = Guid.NewGuid(), TenantId = _tenant, AziendaId = alfa.Id,
            RapportoLavoroId = rAgrOk.Id, Anno = 2026, Mese = 7,
        });
        await db.SaveChangesAsync();
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE Cedolini SET Stato = {nameof(StatoCedolino.Chiusa)} WHERE TenantId = {_tenant} AND Id = {cedolinoChiusoId}");

        var cruscotto = await new CruscottoMeseService(db).CostruisciAsync(_tenant, 2026, 7);

        Assert.Equal(2, cruscotto.AziendeTotali);
        Assert.False(cruscotto.ElencoTagliato);
        Assert.Equal(["Alfa Srl", "Beta Srl"],
            cruscotto.Aziende.Select(a => a.RagioneSociale).ToArray());

        var righeAlfa = cruscotto.Aziende[0].Rapporti;
        // Il cessato a maggio non appartiene al mese di luglio.
        Assert.Equal(6, righeAlfa.Count);
        Assert.DoesNotContain(righeAlfa, r => r.Nominativo.StartsWith("Cessato"));
        string StatoDi(string cognome) =>
            righeAlfa.Single(r => r.Nominativo.StartsWith(cognome)).Stato;
        Assert.Equal("protetto", StatoDi("Chiuso"));
        Assert.Equal("gia_elaborato", StatoDi("Bozza"));
        Assert.Equal("cartellino_presente", StatoDi("Pronto"));
        Assert.Equal("da_compilare", StatoDi("Vuoto"));
        Assert.Equal("gia_elaborato", StatoDi("AgricoloFatto"));
        Assert.Equal("agricolo", StatoDi("AgricoloDaFare"));

        Assert.Equal(3, cruscotto.Aziende[0].Elaborati);
        Assert.Equal(1, cruscotto.Aziende[0].Pronti);
        Assert.Equal(1, cruscotto.Aziende[0].DaCompilare);
        Assert.Equal(0, cruscotto.Aziende[0].Bloccati);
        Assert.Equal(1, cruscotto.Aziende[0].Agricoli);

        // L'azienda senza posizione INPS/PAT/CCNL blocca l'elaborazione, non la nasconde.
        var betaRiga = Assert.Single(cruscotto.Aziende[1].Rapporti);
        Assert.Equal("bloccato", betaRiga.Stato);
        Assert.Equal(1, cruscotto.Aziende[1].Bloccati);
    }

    [Fact]
    public async Task NonMostraAziendeDiAltriTenant_ERifiutaPeriodiInsensati()
    {
        await using var db = NuovoContesto();
        var altroTenant = Guid.NewGuid();
        db.Tenants.Add(new TenantEntity
        {
            Id = _tenant, RagioneSociale = "Studio", CreatoIl = DateTime.UtcNow,
        });
        db.Tenants.Add(new TenantEntity
        {
            Id = altroTenant, RagioneSociale = "Studio estraneo", CreatoIl = DateTime.UtcNow,
        });
        db.Aziende.Add(Azienda(altroTenant, "Estranea Srl", completa: true));
        await db.SaveChangesAsync();

        var servizio = new CruscottoMeseService(db);
        var cruscotto = await servizio.CostruisciAsync(_tenant, 2026, 7);
        Assert.Equal(0, cruscotto.AziendeTotali);
        Assert.Empty(cruscotto.Aziende);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            servizio.CostruisciAsync(_tenant, 2026, 13));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            servizio.CostruisciAsync(_tenant, 1999, 1));
    }
}
