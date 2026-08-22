using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Paghe.Core.AdeTelematico;
using Paghe.Core.Domain;
using Paghe.Persistence;
using Paghe.Persistence.Entities;

namespace Paghe.Tests;

public sealed class CartellinoBozzaServiceTests : IDisposable
{
    private static readonly TimeProvider Oggi15Agosto2026 =
        new OrologioFisso(new DateTimeOffset(2026, 8, 15, 12, 0, 0, TimeSpan.Zero));

    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public CartellinoBozzaServiceTests()
    {
        _connection.Open();
        using var db = NuovoContesto();
        db.Database.EnsureCreated();
    }

    private PagheDbContext NuovoContesto() => new(
        new DbContextOptionsBuilder<PagheDbContext>().UseSqlite(_connection).Options);

    [Fact]
    public async Task CreaBozza_RicalcolaIRileggeCalendarioSenzaCreareCedolino()
    {
        var scenario = await SeedScenarioAsync();
        var calendario = CalendarioCompleto(2026, 7, assenzeNonRetribuite: [12, 13, 14]);

        await using (var db = NuovoContesto())
        {
            var salvata = await Servizio(db).SalvaAsync(
                scenario.TenantId,
                scenario.RapportoId,
                new SalvaCartellinoBozza(
                    NuovoCartellino(2026, 7, calendario),
                    CartellinoIdAtteso: null,
                    VersioneAttesa: 0));

            Assert.NotEqual(Guid.Empty, salvata.Id);
            Assert.Equal(scenario.TenantId, salvata.TenantId);
            Assert.Equal(scenario.AziendaId, salvata.AziendaId);
            Assert.Equal(scenario.RapportoId, salvata.RapportoId);
            Assert.Equal(1, salvata.Versione);
            Assert.Equal(31, salvata.Giornate.Count);
        }

        await using (var verifica = NuovoContesto())
        {
            var entita = await verifica.Cartellini.SingleAsync(c =>
                c.TenantId == scenario.TenantId && c.RapportoLavoroId == scenario.RapportoId);
            Assert.Equal(24m, entita.OreAssenzaNonRetribuita);
            Assert.Equal(1, entita.Versione);
            Assert.Empty(verifica.Cedolini.Where(c => c.TenantId == scenario.TenantId));

            var riga = Assert.Single((await new PresenzeService(verifica).CostruisciAsync(
                scenario.TenantId, scenario.AziendaId, 2026, 7)).Righe);
            Assert.Equal(entita.Id.ToString(), riga.CartellinoId);
            Assert.Equal(1, riga.VersioneCartellino);
            Assert.Equal(24m, riga.OreAssenzaNonRetribuita);

            Assert.Null(await Servizio(verifica).LeggiAsync(
                Guid.NewGuid(), scenario.RapportoId, 2026, 7));
        }
    }

    [Fact]
    public async Task Aggiornamento_UsaIdEVersione_EUnaCopiaObsoletaNonSovrascrive()
    {
        var scenario = await SeedScenarioAsync();
        CartellinoBozzaSalvata prima;

        await using (var db = NuovoContesto())
        {
            prima = await Servizio(db).SalvaAsync(
                scenario.TenantId, scenario.RapportoId,
                new SalvaCartellinoBozza(
                    NuovoCartellino(2026, 7, CalendarioCompleto(2026, 7, [12])), null, 0));
        }

        await using (var db = NuovoContesto())
        {
            var seconda = await Servizio(db).SalvaAsync(
                scenario.TenantId, scenario.RapportoId,
                new SalvaCartellinoBozza(
                    NuovoCartellino(2026, 7, CalendarioCompleto(2026, 7, [12, 13])),
                    prima.Id, prima.Versione));
            Assert.Equal(2, seconda.Versione);
        }

        await using (var db = NuovoContesto())
        {
            var ex = await Assert.ThrowsAsync<ConflittoCartellinoException>(() =>
                Servizio(db).SalvaAsync(
                    scenario.TenantId, scenario.RapportoId,
                    new SalvaCartellinoBozza(
                        NuovoCartellino(2026, 7, CalendarioCompleto(2026, 7, [20])),
                        prima.Id, prima.Versione)));
            Assert.Contains("cambiato", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        await using (var verifica = NuovoContesto())
        {
            var entita = await verifica.Cartellini.SingleAsync(c => c.Id == prima.Id);
            Assert.Equal(2, entita.Versione);
            Assert.Equal(16m, entita.OreAssenzaNonRetribuita);
            Assert.Equal(CausaleGiornata.Lavorata,
                entita.Giornate.Single(g => g.Data.Day == 20).Causale);
        }
    }

    [Fact]
    public async Task CedolinoEsistente_BloccaAncheSeBozza_EConservaIlCartellino()
    {
        var scenario = await SeedScenarioAsync();
        Guid cartellinoId;

        await using (var db = NuovoContesto())
        {
            var salvata = await Servizio(db).SalvaAsync(
                scenario.TenantId, scenario.RapportoId,
                new SalvaCartellinoBozza(
                    NuovoCartellino(2026, 7, CalendarioCompleto(2026, 7, [12])), null, 0));
            cartellinoId = salvata.Id;

            db.Cedolini.Add(new CedolinoEntity
            {
                Id = Guid.NewGuid(), TenantId = scenario.TenantId,
                AziendaId = scenario.AziendaId, RapportoLavoroId = scenario.RapportoId,
                Anno = 2026, Mese = 7, Stato = nameof(StatoCedolino.Bozza), Versione = 1,
            });
            await db.SaveChangesAsync();
        }

        await using (var db = NuovoContesto())
        {
            var ex = await Assert.ThrowsAsync<ArgumentException>(() => Servizio(db).SalvaAsync(
                scenario.TenantId, scenario.RapportoId,
                new SalvaCartellinoBozza(
                    NuovoCartellino(2026, 7, CalendarioCompleto(2026, 7, [12, 13])),
                    cartellinoId, 1)));
            Assert.Contains("cedolino", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        await using (var verifica = NuovoContesto())
        {
            var entita = await verifica.Cartellini.SingleAsync(c => c.Id == cartellinoId);
            Assert.Equal(1, entita.Versione);
            Assert.Equal(8m, entita.OreAssenzaNonRetribuita);
        }
    }

    [Theory]
    [InlineData(StatoPraticaTelematica.InviataManualmente)]
    [InlineData(StatoPraticaTelematica.RicevutaAcquisita)]
    public async Task F24Finale_BloccaLaBozza(StatoPraticaTelematica stato)
    {
        var scenario = await SeedScenarioAsync();
        await using (var db = NuovoContesto())
        {
            db.PraticheF24.Add(new PraticaF24Entity
            {
                Id = Guid.NewGuid(), TenantId = scenario.TenantId,
                AziendaId = scenario.AziendaId, Anno = 2026, Mese = 7,
                Stato = stato.ToString(), CreataIl = DateTime.UtcNow,
                UltimoAggiornamento = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        await using (var db = NuovoContesto())
        {
            var ex = await Assert.ThrowsAsync<ArgumentException>(() => Servizio(db).SalvaAsync(
                scenario.TenantId, scenario.RapportoId,
                new SalvaCartellinoBozza(
                    NuovoCartellino(2026, 7, CalendarioCompleto(2026, 7)), null, 0)));
            Assert.Contains("F24", ex.Message);
            Assert.Empty(db.Cartellini.Where(c => c.TenantId == scenario.TenantId));
        }
    }

    [Fact]
    public async Task RapportoDiAltroTenant_CalendarioIncompletoEMeseFuturo_SonoBloccati()
    {
        var scenario = await SeedScenarioAsync();
        await using var db = NuovoContesto();
        var servizio = Servizio(db);

        await Assert.ThrowsAsync<RapportoCartellinoNonTrovatoException>(() => servizio.SalvaAsync(
            Guid.NewGuid(), scenario.RapportoId,
            new SalvaCartellinoBozza(
                NuovoCartellino(2026, 7, CalendarioCompleto(2026, 7)), null, 0)));

        var incompleto = await Assert.ThrowsAsync<ArgumentException>(() => servizio.SalvaAsync(
            scenario.TenantId, scenario.RapportoId,
            new SalvaCartellinoBozza(
                NuovoCartellino(2026, 7, CalendarioCompleto(2026, 7).Take(30).ToArray()),
                null, 0)));
        Assert.Contains("Mancano", incompleto.Message, StringComparison.OrdinalIgnoreCase);

        var futuro = await Assert.ThrowsAsync<ArgumentException>(() => servizio.SalvaAsync(
            scenario.TenantId, scenario.RapportoId,
            new SalvaCartellinoBozza(
                NuovoCartellino(2026, 9, CalendarioCompleto(2026, 9)), null, 0)));
        Assert.Contains("futuro", futuro.Message, StringComparison.OrdinalIgnoreCase);

        Assert.Empty(db.Cartellini.Where(c => c.TenantId == scenario.TenantId));
    }

    [Fact]
    public async Task MalattiaDerivataDalCalendario_RichiedeUnNumeroEventoValido()
    {
        var scenario = await SeedScenarioAsync();
        var giornate = CalendarioCompleto(2026, 7).ToArray();
        int indice = Array.FindIndex(giornate, g => g.Data.Day == 12);
        giornate[indice] = new GiornataCartellino
        {
            Data = new DateOnly(2026, 7, 12),
            Causale = CausaleGiornata.Malattia,
        };

        await using var db = NuovoContesto();
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => Servizio(db).SalvaAsync(
            scenario.TenantId, scenario.RapportoId,
            new SalvaCartellinoBozza(
                new Cartellino
                {
                    Anno = 2026,
                    Mese = 7,
                    NumeroEventoMalattiaAnno = 0,
                    StatoAssegniFamiliari = StatoAssegniFamiliari.NonSpettanti,
                    Giornate = giornate,
                },
                null, 0)));

        Assert.Contains("numero progressivo", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(db.Cartellini.Where(c => c.TenantId == scenario.TenantId));
    }

    [Fact]
    public async Task EnumSconosciutiEImportiNegativi_NonVengonoPersistiti()
    {
        var scenario = await SeedScenarioAsync();
        await using var db = NuovoContesto();
        var servizio = Servizio(db);

        var statoSconosciuto = await Assert.ThrowsAsync<ArgumentException>(() => servizio.SalvaAsync(
            scenario.TenantId, scenario.RapportoId,
            new SalvaCartellinoBozza(
                new Cartellino
                {
                    Anno = 2026,
                    Mese = 7,
                    StatoAssegniFamiliari = (StatoAssegniFamiliari)99,
                    Giornate = CalendarioCompleto(2026, 7),
                }, null, 0)));
        Assert.Contains("non è riconosciuto", statoSconosciuto.Message);

        var importoNegativo = await Assert.ThrowsAsync<ArgumentException>(() => servizio.SalvaAsync(
            scenario.TenantId, scenario.RapportoId,
            new SalvaCartellinoBozza(
                new Cartellino
                {
                    Anno = 2026,
                    Mese = 7,
                    StatoAssegniFamiliari = StatoAssegniFamiliari.NonSpettanti,
                    FringeBenefit = -1m,
                    Giornate = CalendarioCompleto(2026, 7),
                }, null, 0)));
        Assert.Contains("fringe benefit", importoNegativo.Message, StringComparison.OrdinalIgnoreCase);

        Assert.Empty(db.Cartellini.Where(c => c.TenantId == scenario.TenantId));
    }

    private CartellinoBozzaService Servizio(PagheDbContext db) =>
        new(db, Oggi15Agosto2026);

    private async Task<Scenario> SeedScenarioAsync()
    {
        var scenario = new Scenario(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        await using var db = NuovoContesto();
        db.Aziende.Add(new AziendaEntity
        {
            Id = scenario.AziendaId, TenantId = scenario.TenantId,
            RagioneSociale = "Alfa Srl", Indirizzo = "Via Roma 1",
            Cap = "20100", Comune = "Milano", Provincia = "MI",
        });
        db.Dipendenti.Add(new DipendenteEntity
        {
            Id = scenario.DipendenteId, TenantId = scenario.TenantId,
            CodiceFiscale = "RSSMRA80A01F205X", Nome = "Mario", Cognome = "Rossi",
        });
        db.Rapporti.Add(new RapportoLavoroEntity
        {
            Id = scenario.RapportoId, TenantId = scenario.TenantId,
            AziendaId = scenario.AziendaId, DipendenteId = scenario.DipendenteId,
            CodiceCcnl = "COMMERCIO_TERZIARIO", Livello = "3", Qualifica = "Impiegato",
            DataAssunzione = new DateOnly(2024, 1, 1), OreSettimanali = 40m,
        });
        await db.SaveChangesAsync();
        return scenario;
    }

    private static Cartellino NuovoCartellino(
        int anno, int mese, IReadOnlyList<GiornataCartellino> giornate) => new()
    {
        Anno = anno,
        Mese = mese,
        StatoAssegniFamiliari = StatoAssegniFamiliari.NonSpettanti,
        Giornate = giornate,
    };

    private static IReadOnlyList<GiornataCartellino> CalendarioCompleto(
        int anno, int mese, IReadOnlyCollection<int>? assenzeNonRetribuite = null)
    {
        assenzeNonRetribuite ??= [];
        return Enumerable.Range(1, DateTime.DaysInMonth(anno, mese))
            .Select(giorno =>
            {
                var data = new DateOnly(anno, mese, giorno);
                if (assenzeNonRetribuite.Contains(giorno))
                    return new GiornataCartellino
                    {
                        Data = data,
                        Causale = CausaleGiornata.AssenzaNonRetribuita,
                        OreAssenzaNonRetribuita = 8m,
                    };

                bool domenica = data.DayOfWeek == DayOfWeek.Sunday;
                return new GiornataCartellino
                {
                    Data = data,
                    Causale = domenica
                        ? CausaleGiornata.RiposoSettimanale
                        : CausaleGiornata.Lavorata,
                    OreOrdinarie = domenica ? 0m : 8m,
                };
            })
            .ToArray();
    }

    private sealed record Scenario(
        Guid TenantId, Guid AziendaId, Guid DipendenteId, Guid RapportoId);

    public void Dispose() => _connection.Dispose();
}
