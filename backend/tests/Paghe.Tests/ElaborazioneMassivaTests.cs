using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Paghe.Core.Domain;
using Paghe.Persistence;
using Paghe.Persistence.Entities;
using Xunit;

namespace Paghe.Tests;

/// <summary>
/// «Elabora i pronti»: fedeltà campo per campo della conversione entità→dominio (un campo
/// dimenticato produrrebbe buste diverse da quelle manuali) e comportamento del batch:
/// salta già elaborati e agricoli, un errore non ferma gli altri, tenant isolato.
/// </summary>
public sealed class ElaborazioneMassivaTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly Guid _tenant = Guid.NewGuid();

    public ElaborazioneMassivaTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        using var db = NuovoContesto();
        db.Database.EnsureCreated();
    }

    public void Dispose() => _connection.Dispose();

    private PagheDbContext NuovoContesto() =>
        new(new DbContextOptionsBuilder<PagheDbContext>().UseSqlite(_connection).Options);

    [Fact]
    public void ConversioneEntitaDominio_PreservaOgniCampoDelCartellino()
    {
        var giornate = new List<GiornataCartellino>
        {
            new()
            {
                Data = new DateOnly(2026, 7, 1), Causale = CausaleGiornata.Lavorata,
                OreOrdinarie = 8m, ProgressivoOreSettimanali = 8m,
            },
        };
        var entita = new CartellinoEntity
        {
            Anno = 2026, Mese = 7,
            OreStraordinario = 1.5m, OreStraordinarioNotturno = 2.5m,
            OreStraordinarioFestivo = 3.5m,
            GiorniFerie = 4m, OrePermessi = 5m, OreExFestivita = 6m,
            GiorniMalattia = 7m, NumeroEventoMalattiaAnno = 2, GiorniInfortunio = 8m,
            OreAssenzaNonRetribuita = 9m,
            StatoAssegniFamiliari = nameof(StatoAssegniFamiliari.Spettanti),
            ImportoAssegniFamiliari = 10.10m,
            GiorniTrasfertaItalia = 11m, GiorniTrasfertaEstero = 12m,
            IndennitaTrasfertaGiornaliera = 13.13m,
            RimborsoTrasferta = nameof(TipoRimborsoTrasferta.MistoVittoOAlloggio),
            RimborsiSpeseDocumentate = 14.14m,
            FringeBenefit = 15.15m, FringeBenefitGiaErogatoAnno = 16.16m,
            FigliACarico = true,
            GiorniBuonoPasto = 17, ValoreBuonoPasto = 18.18m, BuonoPastoElettronico = false,
            PremioRisultato = 19.19m, RedditoAnnoPrecedente = 20_000m,
            RiferimentoQuattordicesimaSeparata = "PROSP-14-2026",
            DataPagamentoQuattordicesima = new DateOnly(2026, 7, 15),
            Giornate = giornate,
        };

        var dominio = ElaborazioneMassivaService.CartellinoDaEntita(entita);

        Assert.Equal(2026, dominio.Anno);
        Assert.Equal(7, dominio.Mese);
        Assert.Equal(1.5m, dominio.OreStraordinario);
        Assert.Equal(2.5m, dominio.OreStraordinarioNotturno);
        Assert.Equal(3.5m, dominio.OreStraordinarioFestivo);
        Assert.Equal(4m, dominio.GiorniFerie);
        Assert.Equal(5m, dominio.OrePermessi);
        Assert.Equal(6m, dominio.OreExFestivita);
        Assert.Equal(7m, dominio.GiorniMalattia);
        Assert.Equal(2, dominio.NumeroEventoMalattiaAnno);
        Assert.Equal(8m, dominio.GiorniInfortunio);
        Assert.Equal(9m, dominio.OreAssenzaNonRetribuita);
        Assert.Equal(StatoAssegniFamiliari.Spettanti, dominio.StatoAssegniFamiliari);
        Assert.Equal(10.10m, dominio.ImportoAssegniFamiliari);
        Assert.Equal(11m, dominio.GiorniTrasfertaItalia);
        Assert.Equal(12m, dominio.GiorniTrasfertaEstero);
        Assert.Equal(13.13m, dominio.IndennitaTrasfertaGiornaliera);
        Assert.Equal(TipoRimborsoTrasferta.MistoVittoOAlloggio, dominio.RimborsoTrasferta);
        Assert.Equal(14.14m, dominio.RimborsiSpeseDocumentate);
        Assert.Equal(15.15m, dominio.FringeBenefit);
        Assert.Equal(16.16m, dominio.FringeBenefitGiaErogatoAnno);
        Assert.True(dominio.FigliACarico);
        Assert.Equal(17, dominio.GiorniBuonoPasto);
        Assert.Equal(18.18m, dominio.ValoreBuonoPasto);
        Assert.False(dominio.BuonoPastoElettronico);
        Assert.Equal(19.19m, dominio.PremioRisultato);
        Assert.Equal(20_000m, dominio.RedditoAnnoPrecedente);
        Assert.Equal("PROSP-14-2026", dominio.RiferimentoQuattordicesimaSeparata);
        Assert.Equal(new DateOnly(2026, 7, 15), dominio.DataPagamentoQuattordicesima);
        Assert.Same(giornate, dominio.Giornate);
    }

    [Fact]
    public async Task ElaboraPronti_SaltaElaboratiEAgricoli_EUnErroreNonFermaIlMese()
    {
        await using var db = NuovoContesto();
        db.Tenants.Add(new TenantEntity
        {
            Id = _tenant, RagioneSociale = "Studio", CreatoIl = DateTime.UtcNow,
        });
        var azienda = new AziendaEntity
        {
            Id = Guid.NewGuid(), TenantId = _tenant, RagioneSociale = "Gamma Srl",
        };
        db.Aziende.Add(azienda);

        (DipendenteEntity, RapportoLavoroEntity) Rapporto(string cognome, string ccnl)
        {
            var dipendente = new DipendenteEntity
            {
                Id = Guid.NewGuid(), TenantId = _tenant,
                CodiceFiscale = $"CF{Guid.NewGuid():N}"[..16].ToUpperInvariant(),
                Nome = "Test", Cognome = cognome,
            };
            var rapporto = new RapportoLavoroEntity
            {
                Id = Guid.NewGuid(), TenantId = _tenant, AziendaId = azienda.Id,
                DipendenteId = dipendente.Id, CodiceCcnl = ccnl, Livello = "3",
                Qualifica = "Operaio", TipoContratto = "Tempo indeterminato",
                DataAssunzione = new DateOnly(2025, 1, 1),
            };
            return (dipendente, rapporto);
        }
        var (dPronto, rPronto) = Rapporto("Pronto", "COMMERCIO_TERZIARIO");
        var (dFatto, rFatto) = Rapporto("GiaFatto", "COMMERCIO_TERZIARIO");
        var (dAgricolo, rAgricolo) = Rapporto("Agricolo", "AGRICOLTURA_OPERAI");
        var (dSenza, rSenza) = Rapporto("SenzaCartellino", "COMMERCIO_TERZIARIO");
        db.Dipendenti.AddRange(dPronto, dFatto, dAgricolo, dSenza);
        db.Rapporti.AddRange(rPronto, rFatto, rAgricolo, rSenza);
        // Cartellini per il pronto, il già elaborato e l'agricolo; l'unico candidato vero
        // resta «Pronto», che fallirà (fixture senza calendario né parametri dell'anno).
        foreach (var rapportoId in new[] { rPronto.Id, rFatto.Id, rAgricolo.Id })
            db.Cartellini.Add(new CartellinoEntity
            {
                Id = Guid.NewGuid(), TenantId = _tenant,
                RapportoLavoroId = rapportoId, Anno = 2026, Mese = 7,
            });
        db.Cedolini.Add(new CedolinoEntity
        {
            Id = Guid.NewGuid(), TenantId = _tenant, AziendaId = azienda.Id,
            RapportoLavoroId = rFatto.Id, Anno = 2026, Mese = 7,
            Stato = nameof(StatoCedolino.Bozza),
        });
        await db.SaveChangesAsync();

        var servizio = new ElaborazioneMassivaService(
            db, new CedoliniService(db, new ParametriNormativiService(db)));
        var esito = await servizio.ElaboraProntiAsync(_tenant, azienda.Id, 2026, 7);

        // Un solo candidato tentato: il fallimento torna per riga senza eccezioni di batch.
        Assert.Equal(0, esito.Elaborati);
        Assert.Equal(1, esito.Falliti);
        var riga = Assert.Single(esito.Esiti);
        Assert.StartsWith("Pronto", riga.Nominativo);
        Assert.False(riga.Riuscita);
        Assert.False(string.IsNullOrWhiteSpace(riga.Errore));
        Assert.False(db.Cedolini.Any(c => c.RapportoLavoroId == rPronto.Id));

        // L'azienda di un altro tenant non è raggiungibile.
        await Assert.ThrowsAsync<ArgumentException>(() =>
            servizio.ElaboraProntiAsync(Guid.NewGuid(), azienda.Id, 2026, 7));
    }
}
