using System.Buffers.Binary;
using System.Text.Json;
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
            ProgressivoOreSettimanali = 10m,
        },
        new()
        {
            Data = new DateOnly(2026, 4, 2), Causale = CausaleGiornata.Malattia,
            ProgressivoOreSettimanali = 10m,
            IdentificativoEvento = "MAL-PROT-2026-001",
            DataInizioEvento = new DateOnly(2026, 4, 2),
            RetribuzioneMediaGiornalieraPrecedente = 77.5058m,
            RateiGiornalieri = 6.25m,
            CoefficienteLordizzazione = 1.1234m,
        },
        new()
        {
            Data = new DateOnly(2026, 4, 3), Causale = CausaleGiornata.Lavorata,
            OreOrdinarie = 4m, OrePermessi = 4m,
            ProgressivoOreSettimanali = 14m,
        },
    ];

    private async Task<Guid> SalvaCartellinoAsync(
        IReadOnlyList<GiornataCartellino> giornate, int anno = 2026, int mese = 4)
    {
        var id = Guid.NewGuid();
        await using var db = NuovoContesto();
        db.Cartellini.Add(new CartellinoEntity
        {
            Id = id, TenantId = _tenant, RapportoLavoroId = Guid.NewGuid(),
            Anno = anno, Mese = mese,
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

    private async Task SovrascriviConPayloadLegacyAsync(Guid id, string causale)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(new[]
        {
            new
            {
                G = 1, C = causale, Ord = 8m, Per = 0m, Exf = 0m, Ass = 0m,
                Str = 0m, StrN = 0m, StrF = 0m,
            },
        });
        var payload = new byte[8192];
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(0, sizeof(int)), json.Length);
        json.CopyTo(payload.AsSpan(sizeof(int)));
        string contesto = $"paghe-gdpr:v1|{_tenant:D}|Cartellini|{id:D}|GiornateProtette";
        string cifrato = _protettore.ProteggiConContesto(
            Convert.ToBase64String(payload), contesto)!;

        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = "UPDATE Cartellini SET GiornateProtette = $payload WHERE Id = $id";
        cmd.Parameters.AddWithValue("$payload", cifrato);
        cmd.Parameters.AddWithValue("$id", id);
        await cmd.ExecuteNonQueryAsync();
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
        Assert.Equal(10m, riletto.Giornate[1].ProgressivoOreSettimanali);
        Assert.Equal("MAL-PROT-2026-001", riletto.Giornate[1].IdentificativoEvento);
        Assert.Equal(new DateOnly(2026, 4, 2), riletto.Giornate[1].DataInizioEvento);
        Assert.Equal(77.5058m, riletto.Giornate[1].RetribuzioneMediaGiornalieraPrecedente);
        Assert.Equal(6.25m, riletto.Giornate[1].RateiGiornalieri);
        Assert.Equal(1.1234m, riletto.Giornate[1].CoefficienteLordizzazione);
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
        Assert.DoesNotContain("MAL-PROT-2026-001", grezzo, StringComparison.OrdinalIgnoreCase);
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
                ProgressivoOreSettimanali = 40m,
                IdentificativoEvento = new string('P', 100),
                DataInizioEvento = new DateOnly(2026, 4, 2),
                RetribuzioneMediaGiornalieraPrecedente = 77.5058m,
                RateiGiornalieri = 6.25m,
                CoefficienteLordizzazione = 1.1234m,
            });
        var tanti = await ColonnaGrezzaAsync(await SalvaCartellinoAsync(molti));

        Assert.Equal(pochi.Length, tanti.Length);
    }

    [Fact]
    public async Task IlPayloadContieneTrentunoGiorniConMetadatiUnicodeAlLimite()
    {
        string protocollo = new('\u263A', 100);
        var giornate = Enumerable.Range(1, 31).Select(giorno => new GiornataCartellino
        {
            Data = new DateOnly(2026, 5, giorno),
            Causale = CausaleGiornata.Malattia,
            ProgressivoOreSettimanali = 0m,
            IdentificativoEvento = protocollo,
            DataInizioEvento = new DateOnly(2026, 5, 1),
            RetribuzioneMediaGiornalieraPrecedente = 77.5058m,
            RateiGiornalieri = 6.25m,
            CoefficienteLordizzazione = 1.1234m,
        }).ToArray();

        var id = await SalvaCartellinoAsync(giornate, anno: 2026, mese: 5);

        await using var db = NuovoContesto();
        var riletto = await db.Cartellini.SingleAsync(c => c.Id == id);
        Assert.Equal(31, riletto.Giornate.Count);
        Assert.Equal(protocollo, riletto.Giornate[^1].IdentificativoEvento);
    }

    [Fact]
    public async Task PayloadLegacySenzaNuoviCampi_RestaLeggibileConDefaultNull()
    {
        var id = await SalvaCartellinoAsync([]);
        await SovrascriviConPayloadLegacyAsync(id, "Lavorata");

        await using var db = NuovoContesto();
        var riletto = await db.Cartellini.SingleAsync(c => c.Id == id);
        var giornata = Assert.Single(riletto.Giornate);
        Assert.Equal(8m, giornata.OreOrdinarie);
        Assert.Null(giornata.ProgressivoOreSettimanali);
        Assert.Null(giornata.IdentificativoEvento);
    }

    [Fact]
    public async Task PayloadConCausaleSconosciuta_FallisceChiuso()
    {
        var id = await SalvaCartellinoAsync([]);
        await SovrascriviConPayloadLegacyAsync(id, "CausaleFuturaNonSupportata");

        await using var db = NuovoContesto();
        var errore = await Assert.ThrowsAsync<InvalidOperationException>(
            () => db.Cartellini.SingleAsync(c => c.Id == id));

        Assert.Contains("causale non riconosciuta", errore.Message);
    }
}
