using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Paghe.Core.Domain;
using Paghe.Persistence;
using Paghe.Persistence.Entities;
using Paghe.Persistence.Gdpr;
using Xunit;

namespace Paghe.Tests;

/// <summary>
/// Il calendario giornaliero dice quali giorni sono stati malattia o infortunio: è dato sanitario
/// quanto la terna salute, e nel database deve esistere solo come payload cifrato e legato al
/// tenant. Qui si verifica il giro completo — salvataggio, rilettura, illeggibilità sul disco —
/// e la compatibilità con i cartellini compilati a totali, che di calendario non ne hanno.
/// </summary>
public sealed class CalendarioProtettoTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ProtettoreDati _protettore = new("chiave-di-test-calendario", produzione: false);
    private readonly Guid _tenant = Guid.NewGuid();

    public CalendarioProtettoTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        using var db = NuovoContesto();
        db.Database.EnsureCreated();
    }

    private PagheDbContext NuovoContesto() =>
        new(new DbContextOptionsBuilder<PagheDbContext>().UseSqlite(_connection).Options, _protettore);

    public void Dispose() => _connection.Dispose();

    private static List<GiornataCartellino> TreGiorni() =>
    [
        new()
        {
            Data = new DateOnly(2026, 4, 1), Causale = CausaleGiornata.Lavorata,
            OreOrdinarie = 8m, OreStraordinario = 2m,
        },
        new() { Data = new DateOnly(2026, 4, 2), Causale = CausaleGiornata.Malattia },
        new()
        {
            Data = new DateOnly(2026, 4, 3), Causale = CausaleGiornata.Lavorata,
            OreOrdinarie = 4m, OrePermessi = 4m,
        },
    ];

    private async Task<Guid> SalvaCartellinoAsync(IReadOnlyList<GiornataCartellino> giornate)
    {
        var id = Guid.NewGuid();
        await using var db = NuovoContesto();
        db.Cartellini.Add(new CartellinoEntity
        {
            Id = id, TenantId = _tenant, RapportoLavoroId = Guid.NewGuid(),
            Anno = 2026, Mese = 4,
            GiorniMalattia = 1m, NumeroEventoMalattiaAnno = 1,
            Giornate = giornate,
        });
        await db.SaveChangesAsync();
        return id;
    }

    /// <summary>Legge la colonna com'è sul disco, senza passare dall'interceptor che decifra.</summary>
    private async Task<string> ColonnaGrezzaAsync(Guid cartellinoId)
    {
        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT GiornateProtette FROM Cartellini WHERE Id = $id";
        cmd.Parameters.AddWithValue("$id", cartellinoId);
        return await cmd.ExecuteScalarAsync() as string
            ?? throw new InvalidOperationException("Cartellino non trovato nella lettura grezza.");
    }

    [Fact]
    public async Task IlCalendarioTornaIdenticoDopoIlSalvataggio()
    {
        var id = await SalvaCartellinoAsync(TreGiorni());

        await using var db = NuovoContesto();
        var riletto = await db.Cartellini.SingleAsync(c => c.Id == id);

        Assert.Equal(3, riletto.Giornate.Count);
        Assert.Equal(new DateOnly(2026, 4, 1), riletto.Giornate[0].Data);
        Assert.Equal(8m, riletto.Giornate[0].OreOrdinarie);
        Assert.Equal(2m, riletto.Giornate[0].OreStraordinario);
        Assert.Equal(CausaleGiornata.Malattia, riletto.Giornate[1].Causale);
        Assert.Equal(4m, riletto.Giornate[2].OrePermessi);
    }

    [Fact]
    public async Task SulDiscoLaMalattiaNonSiLegge()
    {
        var id = await SalvaCartellinoAsync(TreGiorni());

        var grezzo = await ColonnaGrezzaAsync(id);

        Assert.StartsWith(_protettore.PrefissoChiaveCorrente, grezzo);
        Assert.DoesNotContain("Malattia", grezzo, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Lavorata", grezzo, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CartellinoSenzaCalendario_RestaValidoEConColonnaVuota()
    {
        var id = await SalvaCartellinoAsync([]);

        Assert.Equal("", await ColonnaGrezzaAsync(id));

        await using var db = NuovoContesto();
        var riletto = await db.Cartellini.SingleAsync(c => c.Id == id);
        Assert.Empty(riletto.Giornate);
    }

    [Fact]
    public async Task DueSalvataggiUguali_ProduconoCifratiDiversi()
    {
        var primo = await ColonnaGrezzaAsync(await SalvaCartellinoAsync(TreGiorni()));
        var secondo = await ColonnaGrezzaAsync(await SalvaCartellinoAsync(TreGiorni()));

        Assert.NotEqual(primo, secondo);
    }

    [Fact]
    public async Task IlPayloadHaSempreLaStessaLunghezza_ENonRivelaQuantiGiorniCiSiano()
    {
        // Se il cifrato fosse più corto per i mesi con poche assenze, la sola lunghezza
        // racconterebbe qualcosa sulla salute della persona.
        var pochi = await ColonnaGrezzaAsync(await SalvaCartellinoAsync(TreGiorni()));

        var molti = TreGiorni();
        for (int g = 4; g <= 30; g++)
            molti.Add(new GiornataCartellino
            {
                Data = new DateOnly(2026, 4, g), Causale = CausaleGiornata.Malattia,
            });
        var tanti = await ColonnaGrezzaAsync(await SalvaCartellinoAsync(molti));

        Assert.Equal(pochi.Length, tanti.Length);
    }
}
