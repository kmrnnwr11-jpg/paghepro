using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Paghe.Core.AdeTelematico.UniEmens;
using Paghe.Core.Domain;
using Paghe.Persistence;
using Paghe.Persistence.Entities;

namespace Paghe.Tests.AdeTelematico;

/// <summary>
/// Integrazione su SQLite: la denuncia UniEmens del mese nasce dai cedolini archiviati e
/// dall'anagrafica. Verifica anche il patto con l'utente: i dati che il tracciato pretende e
/// che l'archivio non ha vengono dichiarati, non inventati.
/// </summary>
public sealed class UniEmensDelMeseDbTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly Guid _tenant = Guid.NewGuid();
    private readonly Guid _azienda = Guid.NewGuid();
    private readonly Guid _rapporto = Guid.NewGuid();
    private readonly Guid _dipendente = Guid.NewGuid();

    public UniEmensDelMeseDbTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        using var db = NuovoContesto();
        db.Database.EnsureCreated();
    }

    private PagheDbContext NuovoContesto() =>
        new(new DbContextOptionsBuilder<PagheDbContext>().UseSqlite(_connection).Options);

    /// <summary>Prepara azienda, lavoratore e cedolino chiuso del mese.</summary>
    private async Task PreparaAsync(
        Action<RapportoLavoroEntity>? inquadramento = null,
        bool creaCartellino = true,
        Action<CedolinoEntity>? configuraCedolino = null)
    {
        await using var db = NuovoContesto();
        db.Aziende.Add(new AziendaEntity
        {
            Id = _azienda, TenantId = _tenant, RagioneSociale = "ALFA SRL",
            PartitaIva = "01567890122", CodiceFiscale = "01567890122",
            Indirizzo = "Via Roma 1", Cap = "20100", Comune = "Milano", Provincia = "MI",
            Settore = "COMMERCIO_SERVIZI", CodiceAteco = "47.91.10",
            MatricolaInps = "1234567890", CodiceSedeInps = "7100", CscInps = "10101",
            CodiciAutorizzazioneInps = "NESSUNO",
            CodiceFondoIntegrazioneSalariale = "FIS",
            CodiceAgevolazioneContributiva = "NESSUNA",
            AliquotaInpsDipendente = 0.0919m,
            AliquotaInpsDatore = 0.30m,
            FonteProfiloContributivo = "Fixture UniEmens: profilo contributivo verificato",
            PatInail = "12345678/90", CcnlDefault = "COMMERCIO_TERZIARIO", LivelloDefault = "3",
            LegaleRappresentanteCodiceFiscale = "RSSMRA85M01H501Q",
        });
        db.Dipendenti.Add(new DipendenteEntity
        {
            Id = _dipendente, TenantId = _tenant,
            CodiceFiscale = "VRDLGI90A01F205J", Nome = "Luigi", Cognome = "Verdi",
        });

        var rapporto = new RapportoLavoroEntity
        {
            Id = _rapporto, TenantId = _tenant, AziendaId = _azienda, DipendenteId = _dipendente,
            CodiceCcnl = "COMMERCIO_TERZIARIO", Livello = "3", Qualifica = "Impiegato",
            TipoContratto = "Tempo indeterminato", OreSettimanali = 40m, NumeroMensilita = 13,
            Regione = "12", CodiceCatastaleComune = "F205",
            DataAssunzione = new DateOnly(2020, 1, 1),
        };
        inquadramento?.Invoke(rapporto);
        db.Rapporti.Add(rapporto);

        if (creaCartellino)
        {
            db.Cartellini.Add(new CartellinoEntity
            {
                Id = Guid.NewGuid(), TenantId = _tenant, RapportoLavoroId = _rapporto,
                Anno = 2025, Mese = 12,
                Giornate = CalendarioCompleto(),
                StatoAssegniFamiliari = nameof(StatoAssegniFamiliari.NonSpettanti),
            });
        }

        var cedolino = new CedolinoEntity
        {
            Id = Guid.NewGuid(), TenantId = _tenant, AziendaId = _azienda,
            RapportoLavoroId = _rapporto, Anno = 2025, Mese = 12,
            ImponibileInps = 1_800.40m, ContributiDipendente = 165.64m, ContributiDatore = 540.15m,
            CodiceCnel = "H011", CalcolatoIl = DateTime.UtcNow,
            Stato = nameof(StatoCedolino.Bozza), Versione = 1,
            Voci =
            [
                new CedolinoVoceEntity
                {
                    Id = Guid.NewGuid(), TenantId = _tenant, Ordine = 1, Codice = "PAGA_BASE",
                    Descrizione = "Minimo tabellare", Tipo = nameof(TipoVoce.Competenza),
                    Importo = 1_600m,
                },
                new CedolinoVoceEntity
                {
                    Id = Guid.NewGuid(), TenantId = _tenant, Ordine = 2, Codice = "SUPERMIN",
                    Descrizione = "Superminimo", Tipo = nameof(TipoVoce.Competenza),
                    Importo = 150m,
                },
                new CedolinoVoceEntity
                {
                    Id = Guid.NewGuid(), TenantId = _tenant, Ordine = 3, Codice = "STRAORD",
                    Descrizione = "Straordinario", Tipo = nameof(TipoVoce.Competenza),
                    Importo = 50.40m,
                },
            ],
        };
        configuraCedolino?.Invoke(cedolino);
        db.Cedolini.Add(cedolino);
        await db.SaveChangesAsync();
        foreach (var cedolinoArchiviato in db.Cedolini.Local)
            await db.ClassificaPreWorkflowAsync(_tenant, cedolinoArchiviato.Id);
    }

    private static IReadOnlyList<GiornataCartellino> CalendarioCompleto()
    {
        decimal progressivo = 0m;
        return Enumerable.Range(1, 31).Select(giorno =>
        {
            var data = new DateOnly(2025, 12, giorno);
            if (data.DayOfWeek == DayOfWeek.Monday) progressivo = 0m;
            bool lavorata = data.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday;
            if (lavorata) progressivo += 8m;
            return new GiornataCartellino
            {
                Data = data,
                Causale = lavorata
                    ? CausaleGiornata.Lavorata
                    : CausaleGiornata.RiposoSettimanale,
                OreOrdinarie = lavorata ? 8m : 0m,
                ProgressivoOreSettimanali = progressivo,
            };
        }).ToList();
    }

    [Fact]
    public async Task Denuncia_del_mese_conforme_allo_schema_ufficiale()
    {
        await PreparaAsync(r =>
        {
            r.CittadinanzaInps = "100";
            r.QualificaProfessionaleIstat = "4.1.1.2.0";
        });

        await using var db = NuovoContesto();
        var esito = await new UniEmensService(db)
            .GeneraDelMeseAsync(_tenant, _azienda, 2025, 12, "1234567890");

        Assert.Empty(esito.ErroriSchema);
        var lavoratore = Assert.Single(esito.Flusso.Azienda.Posizione.Lavoratori);
        Assert.Equal("VRDLGI90A01F205J", lavoratore.CodiceFiscale);
        Assert.Equal("2", lavoratore.Qualifica1);          // impiegato
        Assert.Equal("F", lavoratore.Qualifica2);          // tempo pieno
        Assert.Equal("I", lavoratore.Qualifica3);          // tempo indeterminato
        Assert.Equal("100", lavoratore.Cittadinanza);
        Assert.Equal("H011", lavoratore.CodiceContratto);  // codice CNEL del cedolino
        Assert.Equal("F205", lavoratore.CodiceComune);
        Assert.Equal(4000, lavoratore.OrarioContrattuale); // 40 ore in centesimi
        Assert.Equal(13000, lavoratore.NumMensilita);
    }

    /// <summary>
    /// La retribuzione teorica è obbligatoria per l'INPS ed è la retribuzione contrattuale:
    /// le voci fisse della busta, senza straordinari e altri elementi variabili.
    /// </summary>
    [Fact]
    public async Task Retribuzione_teorica_esclude_gli_elementi_variabili()
    {
        await PreparaAsync(r => r.CittadinanzaInps = "100");

        await using var db = NuovoContesto();
        var esito = await new UniEmensService(db)
            .GeneraDelMeseAsync(_tenant, _azienda, 2025, 12, "1234567890");

        var retribuzione = esito.Flusso.Azienda.Posizione.Lavoratori.Single().Retribuzione;
        Assert.Equal(1_750m, retribuzione.RetribTeorica);   // 1.600 + 150, senza lo straordinario
        Assert.Equal(1_800.40m, retribuzione.Imponibile);
    }

    [Fact]
    public async Task Cittadinanza_mancante_viene_dichiarata_non_inventata()
    {
        await PreparaAsync();

        await using var db = NuovoContesto();
        var esito = await new UniEmensService(db)
            .GeneraDelMeseAsync(_tenant, _azienda, 2025, 12, "1234567890");

        Assert.Null(esito.Flusso.Azienda.Posizione.Lavoratori.Single().Cittadinanza);
        Assert.Contains(esito.DaCompletare, c => c.Campo.Contains("Cittadinanza"));
        // Anche incompleto il file resta conforme allo schema: a mancare è un dato, non la forma.
        Assert.Empty(esito.ErroriSchema);
    }

    [Fact]
    public async Task Part_time_senza_tipo_viene_dichiarato()
    {
        await PreparaAsync(r =>
        {
            r.PartTime = true;
            r.PercentualePartTime = 50m;
            r.CittadinanzaInps = "100";
        });

        await using var db = NuovoContesto();
        var esito = await new UniEmensService(db)
            .GeneraDelMeseAsync(_tenant, _azienda, 2025, 12, "1234567890");

        Assert.Null(esito.Flusso.Azienda.Posizione.Lavoratori.Single().Qualifica2);
        Assert.Contains(esito.DaCompletare, c => c.Campo.Contains("part-time"));
    }

    [Fact]
    public async Task Part_time_con_tipo_finisce_nel_flusso()
    {
        await PreparaAsync(r =>
        {
            r.PartTime = true;
            r.PercentualePartTime = 50m;
            r.TipoPartTimeInps = "V";
            r.CittadinanzaInps = "100";
        });

        await using var db = NuovoContesto();
        var esito = await new UniEmensService(db)
            .GeneraDelMeseAsync(_tenant, _azienda, 2025, 12, "1234567890");

        var lavoratore = esito.Flusso.Azienda.Posizione.Lavoratori.Single();
        Assert.Equal("V", lavoratore.Qualifica2);
        Assert.Equal(5000, lavoratore.PercPartTime);
        Assert.DoesNotContain(esito.DaCompletare, c => c.Campo.Contains("part-time"));
        Assert.Empty(esito.ErroriSchema);
    }

    [Fact]
    public async Task Cartellino_mancante_blocca_senza_generare_un_calendario_fittizio()
    {
        await PreparaAsync(r => r.CittadinanzaInps = "100", creaCartellino: false);

        await using var db = NuovoContesto();
        var errore = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new UniEmensService(db)
                .GeneraDelMeseAsync(_tenant, _azienda, 2025, 12, "1234567890"));

        Assert.Contains("manca il cartellino reale", errore.Message);
    }

    [Fact]
    public async Task Cartellino_incompleto_blocca_senza_completare_i_giorni()
    {
        await PreparaAsync(r => r.CittadinanzaInps = "100");
        await using (var db = NuovoContesto())
        {
            var cartellino = await db.Cartellini.SingleAsync(c =>
                c.TenantId == _tenant && c.RapportoLavoroId == _rapporto);
            cartellino.Giornate = cartellino.Giornate.Where(g => g.Data.Day != 15).ToList();
            await db.SaveChangesAsync();
        }

        await using var verifica = NuovoContesto();
        var errore = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new UniEmensService(verifica)
                .GeneraDelMeseAsync(_tenant, _azienda, 2025, 12, "1234567890"));
        Assert.Contains("una sola riga reale", errore.Message);
    }

    [Theory]
    [InlineData("identificativo")]
    [InlineData("data")]
    [InlineData("rmg")]
    [InlineData("ratei")]
    [InlineData("lordizzazione")]
    public async Task Malattia_con_metadato_obbligatorio_mancante_blocca(string campo)
    {
        await PreparaAsync(r => r.CittadinanzaInps = "100");
        await using (var db = NuovoContesto())
        {
            var cartellino = await db.Cartellini.SingleAsync(c =>
                c.TenantId == _tenant && c.RapportoLavoroId == _rapporto);
            cartellino.GiorniMalattia = 1m;
            cartellino.Giornate = cartellino.Giornate.Select(g => g.Data.Day == 8
                ? g with
                {
                    Causale = CausaleGiornata.Malattia,
                    OreOrdinarie = 0m,
                    IdentificativoEvento = campo == "identificativo" ? null : "CM:1234567890",
                    DataInizioEvento = campo == "data" ? null : new DateOnly(2025, 12, 8),
                    RetribuzioneMediaGiornalieraPrecedente = campo == "rmg" ? null : 75m,
                    RateiGiornalieri = campo == "ratei" ? null : 5m,
                    CoefficienteLordizzazione = campo == "lordizzazione" ? null : 1.1m,
                }
                : g).ToList();
            await db.SaveChangesAsync();
        }

        await using var verifica = NuovoContesto();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new UniEmensService(verifica)
                .GeneraDelMeseAsync(_tenant, _azienda, 2025, 12, "1234567890"));
    }

    [Fact]
    public async Task Totale_malattia_senza_dettaglio_giornaliero_blocca()
    {
        await PreparaAsync(r => r.CittadinanzaInps = "100");
        await using (var db = NuovoContesto())
        {
            var cartellino = await db.Cartellini.SingleAsync(c =>
                c.TenantId == _tenant && c.RapportoLavoroId == _rapporto);
            cartellino.GiorniMalattia = 1m;
            await db.SaveChangesAsync();
        }

        await using var verifica = NuovoContesto();
        var errore = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new UniEmensService(verifica)
                .GeneraDelMeseAsync(_tenant, _azienda, 2025, 12, "1234567890"));
        Assert.Contains("dettaglio giornaliero reale", errore.Message);
    }

    [Fact]
    public async Task Malattia_completa_espone_giorni_settimana_e_differenza_accredito()
    {
        const string puc = "12345678901234567890";
        await PreparaAsync(
            r => r.CittadinanzaInps = "100",
            configuraCedolino: cedolino => cedolino.Voci.AddRange(
            [
                Voce(cedolino.Id, 10, "MAL_STORNO", -500m),
                Voce(cedolino.Id, 11, "MAL_CARENZA", 150m),
                Voce(cedolino.Id, 12, "MAL_INPS", 200m),
            ]));
        await using (var db = NuovoContesto())
        {
            var cartellino = await db.Cartellini.SingleAsync(c =>
                c.TenantId == _tenant && c.RapportoLavoroId == _rapporto);
            cartellino.GiorniMalattia = 7m;
            cartellino.Giornate = cartellino.Giornate.Select(g => g.Data.Day is >= 8 and <= 14
                ? g with
                {
                    Causale = CausaleGiornata.Malattia,
                    OreOrdinarie = 0m,
                    IdentificativoEvento = $"CM:{puc}",
                    DataInizioEvento = new DateOnly(2025, 12, 8),
                    RetribuzioneMediaGiornalieraPrecedente = 75m,
                    RateiGiornalieri = 5m,
                    CoefficienteLordizzazione = 1.1m,
                }
                : g).ToList();

            await db.SaveChangesAsync();
        }

        await using var verifica = NuovoContesto();
        var esito = await new UniEmensService(verifica)
            .GeneraDelMeseAsync(_tenant, _azienda, 2025, 12, "1234567890");

        Assert.Empty(esito.ErroriSchema);
        var retribuzione = esito.Flusso.Azienda.Posizione.Lavoratori.Single().Retribuzione;
        Assert.Contains(retribuzione.Settimane,
            s => s.Id == 50 && s.TipoCopertura == "1" && s.CodiciEvento.Contains("MAL"));
        Assert.Equal(350m, Assert.Single(retribuzione.DifferenzeAccredito).Importo);
        var giorno = retribuzione.Giorni.Single(g => g.Giorno == 11);
        Assert.Equal("1", giorno.TipoCopertura);
        var evento = Assert.Single(giorno.Eventi);
        Assert.Equal("MAL", evento.CodiceEvento);
        Assert.Equal("CM", evento.TipoInformazioneAggiuntiva);
        Assert.Equal(puc, evento.InformazioneAggiuntiva);
    }

    [Fact]
    public async Task Infortunio_ordinario_espone_settimana_e_differenza_senza_inventare_evento_giornaliero()
    {
        await PreparaAsync(
            r => r.CittadinanzaInps = "100",
            configuraCedolino: cedolino => cedolino.Voci.AddRange(
            [
                Voce(cedolino.Id, 10, "INF_STORNO", -600m),
                Voce(cedolino.Id, 11, "INF_EVENTO", 100m),
                Voce(cedolino.Id, 12, "INF_CARENZA", 180m),
                Voce(cedolino.Id, 13, "INF_INAIL", 270m),
            ]));
        await using (var db = NuovoContesto())
        {
            var cartellino = await db.Cartellini.SingleAsync(c =>
                c.TenantId == _tenant && c.RapportoLavoroId == _rapporto);
            cartellino.GiorniInfortunio = 6m;
            cartellino.Giornate = cartellino.Giornate.Select(g => g.Data.Day is >= 8 and <= 13
                ? g with
                {
                    Causale = CausaleGiornata.Infortunio,
                    OreOrdinarie = 0m,
                    IdentificativoEvento = "INAIL:123456789",
                    DataInizioEvento = new DateOnly(2025, 12, 8),
                    RetribuzioneMediaGiornalieraPrecedente = 75m,
                    RateiGiornalieri = 5m,
                    CoefficienteLordizzazione = 1m,
                }
                : g).ToList();
            await db.SaveChangesAsync();
        }

        await using var verifica = NuovoContesto();
        var esito = await new UniEmensService(verifica)
            .GeneraDelMeseAsync(_tenant, _azienda, 2025, 12, "1234567890");

        Assert.Empty(esito.ErroriSchema);
        var retribuzione = esito.Flusso.Azienda.Posizione.Lavoratori.Single().Retribuzione;
        Assert.Contains(retribuzione.Settimane,
            s => s.Id == 50 && s.TipoCopertura == "2" && s.CodiciEvento.Contains("INF"));
        Assert.Equal(320m, Assert.Single(retribuzione.DifferenzeAccredito).Importo);
        Assert.All(retribuzione.Giorni.Where(g => g.Giorno is >= 8 and <= 13),
            giorno => Assert.Empty(giorno.Eventi));
    }

    [Fact]
    public async Task Altro_tenant_non_vede_la_denuncia()
    {
        await PreparaAsync(r => r.CittadinanzaInps = "100");

        await using var db = NuovoContesto();
        await Assert.ThrowsAsync<ArgumentException>(() => new UniEmensService(db)
            .GeneraDelMeseAsync(Guid.NewGuid(), _azienda, 2025, 12, "1234567890"));
    }

    private CedolinoVoceEntity Voce(Guid cedolinoId, int ordine, string codice, decimal importo) =>
        new()
        {
            Id = Guid.NewGuid(), TenantId = _tenant, CedolinoId = cedolinoId,
            Ordine = ordine, Codice = codice, Descrizione = codice,
            Tipo = nameof(TipoVoce.Competenza), Importo = importo,
        };

    public void Dispose() => _connection.Dispose();
}
