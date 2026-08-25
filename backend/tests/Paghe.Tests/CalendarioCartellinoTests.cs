using System.Globalization;
using Paghe.Core.Domain;
using Xunit;

namespace Paghe.Tests;

/// <summary>
/// Contratto fail-closed del calendario: ogni giorno è presente, le quantità sono esclusive,
/// il progressivo ISO è verificabile e gli eventi sanitari hanno dati e sequenza completi.
/// </summary>
public sealed class CalendarioCartellinoTests
{
    private const decimal OreGiorno = 8m;
    private const int Anno = 2026;
    private const int Mese = 4;

    private static Cartellino Mese2026() => new() { Anno = Anno, Mese = Mese };

    private static GiornataCartellino Giorno(
        int giorno, CausaleGiornata causale, decimal ore = 0m) => new()
    {
        Data = new DateOnly(Anno, Mese, giorno),
        Causale = causale,
        OreOrdinarie = ore,
    };

    private static GiornataCartellino Evento(
        int giorno,
        CausaleGiornata causale,
        int inizio,
        string identificativo = "EVT-2026-0001") => new()
    {
        Data = new DateOnly(Anno, Mese, giorno),
        Causale = causale,
        IdentificativoEvento = identificativo,
        DataInizioEvento = new DateOnly(Anno, Mese, inizio),
        RetribuzioneMediaGiornalieraPrecedente = 77.5058m,
        RateiGiornalieri = 6.25m,
        CoefficienteLordizzazione = 1.1234m,
    };

    private static List<GiornataCartellino> MeseCompleto(
        int anno = Anno, int mese = Mese, decimal riportoPrimaSettimana = 0m)
    {
        var giornate = new List<GiornataCartellino>();
        for (int giorno = 1; giorno <= DateTime.DaysInMonth(anno, mese); giorno++)
        {
            var data = new DateOnly(anno, mese, giorno);
            bool feriale = data.DayOfWeek is not DayOfWeek.Saturday
                and not DayOfWeek.Sunday;
            giornate.Add(new GiornataCartellino
            {
                Data = data,
                Causale = feriale
                    ? CausaleGiornata.Lavorata
                    : CausaleGiornata.RiposoSettimanale,
                OreOrdinarie = feriale ? OreGiorno : 0m,
            });
        }

        RicalcolaProgressivi(giornate, riportoPrimaSettimana);
        return giornate;
    }

    /// <summary>
    /// Popola il dato ridondante che arriva dalla rilevazione presenze. Per la prima settimana
    /// del mese può includere le ore lavorate nel mese precedente.
    /// </summary>
    private static void RicalcolaProgressivi(
        List<GiornataCartellino> giornate, decimal riportoPrimaSettimana = 0m)
    {
        (int Anno, int Settimana)? settimanaPrecedente = null;
        decimal progressivo = 0m;
        bool primaSettimana = true;

        foreach (int indice in Enumerable.Range(0, giornate.Count)
            .OrderBy(i => giornate[i].Data))
        {
            var g = giornate[indice];
            var data = g.Data.ToDateTime(TimeOnly.MinValue);
            var settimana = (ISOWeek.GetYear(data), ISOWeek.GetWeekOfYear(data));
            if (settimanaPrecedente != settimana)
            {
                progressivo = primaSettimana && g.Data.DayOfWeek != DayOfWeek.Monday
                    ? riportoPrimaSettimana
                    : 0m;
                settimanaPrecedente = settimana;
                primaSettimana = false;
            }

            progressivo += g.TotaleOreLavorate;
            giornate[indice] = g with { ProgressivoOreSettimanali = progressivo };
        }
    }

    private static void Sostituisci(
        List<GiornataCartellino> giornate,
        int giorno,
        GiornataCartellino nuova)
    {
        int indice = giornate.FindIndex(g => g.Data.Day == giorno);
        giornate[indice] = nuova;
        RicalcolaProgressivi(giornate);
    }

    private static void Sostituisci(
        List<GiornataCartellino> giornate,
        int giorno,
        CausaleGiornata causale,
        decimal ore = 0m) =>
        Sostituisci(giornate, giorno, Giorno(giorno, causale, ore));

    private static void AssenzaIntera(List<GiornataCartellino> giornate, int giorno) =>
        Sostituisci(giornate, giorno, new GiornataCartellino
        {
            Data = new DateOnly(Anno, Mese, giorno),
            Causale = CausaleGiornata.AssenzaNonRetribuita,
            OreAssenzaNonRetribuita = OreGiorno,
        });

    private static IReadOnlyList<ProblemaCartellino> Verifica(
        IReadOnlyList<GiornataCartellino> giornate,
        int anno = Anno,
        int mese = Mese,
        decimal limite = CalendarioCartellino.LimiteOreSettimanaliPrudenzialePredefinito,
        decimal? orePregresse = 0m) =>
        CalendarioCartellino.Verifica(
            anno, mese, giornate, OreGiorno, limite, orePregresse);

    private static Cartellino Compila(
        Cartellino dati,
        IReadOnlyList<GiornataCartellino> giornate,
        decimal? orePregresse = 0m) =>
        CalendarioCartellino.Compila(
            dati,
            giornate,
            OreGiorno,
            CalendarioCartellino.LimiteOreSettimanaliPrudenzialePredefinito,
            orePregresse);

    [Fact]
    public void CalendarioVuoto_EBloccante()
    {
        var problemi = Verifica([]);

        Assert.Contains(problemi, p =>
            p.Bloccante && p.Messaggio.Contains("soli totali", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void GiorniScoperti_SonoBloccanti()
    {
        var giornate = MeseCompleto();
        giornate.RemoveAll(g => g.Data.Day is 7 or 8);

        var problemi = Verifica(giornate);

        var problema = Assert.Single(problemi, p => p.Messaggio.Contains("Mancano"));
        Assert.True(problema.Bloccante);
        Assert.Contains("07/04", problema.Messaggio);
        Assert.Contains("08/04", problema.Messaggio);
    }

    [Fact]
    public void GiornoIndicatoDueVolte_EBloccante()
    {
        var giornate = MeseCompleto();
        giornate.Add(giornate.Single(g => g.Data.Day == 10));

        var problemi = Verifica(giornate);

        Assert.Contains(problemi, p => p.Bloccante && p.Messaggio.Contains("più di una volta"));
    }

    [Fact]
    public void CausaleNumericaSconosciuta_EBloccante()
    {
        var giornate = MeseCompleto();
        giornate[0] = giornate[0] with { Causale = (CausaleGiornata)99 };

        var problemi = Verifica(giornate);

        Assert.Contains(problemi, p =>
            p.Bloccante && p.Messaggio.Contains("non riconosciuta", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void GiornataDiUnAltroMese_EBloccante()
    {
        var giornate = MeseCompleto();
        giornate.Add(new GiornataCartellino
        {
            Data = new DateOnly(Anno, 5, 1),
            Causale = CausaleGiornata.Lavorata,
            OreOrdinarie = OreGiorno,
            ProgressivoOreSettimanali = OreGiorno,
        });

        var problemi = Verifica(giornate);

        Assert.Contains(problemi, p => p.Bloccante && p.Messaggio.Contains("non appartiene al mese"));
    }

    [Fact]
    public void GiornataLavorataSenzaOre_EBloccante()
    {
        var giornate = MeseCompleto();
        Sostituisci(giornate, 6, CausaleGiornata.Lavorata);

        var problemi = Verifica(giornate);

        Assert.Contains(problemi, p =>
            p.Bloccante && p.Messaggio.Contains("non indica ore lavorate"));
    }

    [Fact]
    public void GiornataLavorata_DeveQuadrareLeOreTeoriche()
    {
        var giornate = MeseCompleto();
        Sostituisci(giornate, 6, CausaleGiornata.Lavorata, ore: 7m);

        var problemi = Verifica(giornate);

        Assert.Contains(problemi, p =>
            p.Bloccante && p.Messaggio.Contains("totale indicato non coincide"));
    }

    [Fact]
    public void LavoroConRolParziale_CoerenteEConsentito()
    {
        var giornate = MeseCompleto();
        Sostituisci(giornate, 6, new GiornataCartellino
        {
            Data = new DateOnly(Anno, Mese, 6),
            Causale = CausaleGiornata.Lavorata,
            OreOrdinarie = 4m,
            OrePermessi = 4m,
        });

        var problemi = Verifica(giornate);

        Assert.DoesNotContain(problemi, p => p.Bloccante);
    }

    [Fact]
    public void LavoroConRolEStraordinario_EBloccante()
    {
        var giornate = MeseCompleto();
        Sostituisci(giornate, 6, new GiornataCartellino
        {
            Data = new DateOnly(Anno, Mese, 6),
            Causale = CausaleGiornata.Lavorata,
            OreOrdinarie = 4m,
            OrePermessi = 4m,
            OreStraordinario = 1m,
        });

        var problemi = Verifica(giornate);

        Assert.Contains(problemi, p =>
            p.Bloccante && p.Messaggio.Contains("ROL") && p.Messaggio.Contains("straordinario"));
    }

    [Theory]
    [InlineData(CausaleGiornata.Ferie)]
    [InlineData(CausaleGiornata.Malattia)]
    [InlineData(CausaleGiornata.Infortunio)]
    [InlineData(CausaleGiornata.RiposoSettimanale)]
    [InlineData(CausaleGiornata.Festivita)]
    public void CausaleAGiornataConOreOExtra_EBloccante(CausaleGiornata causale)
    {
        var giornate = MeseCompleto();
        var g = causale is CausaleGiornata.Malattia or CausaleGiornata.Infortunio
            ? Evento(6, causale, 6) with { OreStraordinario = 1m }
            : Giorno(6, causale) with { OreStraordinario = 1m };
        Sostituisci(giornate, 6, g);

        var problemi = Verifica(giornate);

        Assert.Contains(problemi, p =>
            p.Bloccante && p.Messaggio.Contains("incompatibili", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(CausaleGiornata.PermessoRetribuito)]
    [InlineData(CausaleGiornata.ExFestivita)]
    [InlineData(CausaleGiornata.AssenzaNonRetribuita)]
    public void CausaleAOre_RichiedeLaGiornataTeoricaIntera(CausaleGiornata causale)
    {
        var giornate = MeseCompleto();
        var g = Giorno(6, causale) with
        {
            OrePermessi = causale == CausaleGiornata.PermessoRetribuito ? 4m : 0m,
            OreExFestivita = causale == CausaleGiornata.ExFestivita ? 4m : 0m,
            OreAssenzaNonRetribuita =
                causale == CausaleGiornata.AssenzaNonRetribuita ? 4m : 0m,
        };
        Sostituisci(giornate, 6, g);

        var problemi = Verifica(giornate);

        Assert.Contains(problemi, p =>
            p.Bloccante && p.Messaggio.Contains("deve indicare 8 ore"));
    }

    [Fact]
    public void ProgressivoSettimanaleMancante_EBloccante()
    {
        var giornate = MeseCompleto();
        giornate[10] = giornate[10] with { ProgressivoOreSettimanali = null };

        var problemi = Verifica(giornate);

        Assert.Contains(problemi, p =>
            p.Bloccante && p.Messaggio.Contains("non riporta il progressivo"));
    }

    [Fact]
    public void ProgressivoSettimanaleNonCoerente_EBloccante()
    {
        var giornate = MeseCompleto();
        giornate[7] = giornate[7] with
        {
            ProgressivoOreSettimanali = giornate[7].ProgressivoOreSettimanali + 1m,
        };

        var problemi = Verifica(giornate);

        Assert.Contains(problemi, p =>
            p.Bloccante && p.Messaggio.Contains("dalle giornate risulta"));
    }

    [Fact]
    public void ProgressivoPrimaSettimana_EreditaLeOreDelMesePrecedente()
    {
        // Luglio 2026 inizia di mercoledì: lunedì e martedì della stessa settimana sono a giugno.
        var giornate = MeseCompleto(2026, 7, riportoPrimaSettimana: 16m);

        var problemi = Verifica(giornate, anno: 2026, mese: 7, orePregresse: 16m);

        Assert.DoesNotContain(problemi, p => p.Bloccante);
        Assert.Equal(24m, giornate[0].ProgressivoOreSettimanali);
    }

    [Fact]
    public void RiportoPrimaSettimanaMancante_EBloccanteSeIlMeseNonIniziaLunedi()
    {
        var giornate = MeseCompleto(2026, 7, riportoPrimaSettimana: 16m);

        var problemi = Verifica(
            giornate, anno: 2026, mese: 7, orePregresse: null);

        Assert.Contains(problemi, p =>
            p.Bloccante && p.Messaggio.Contains("Manca il riporto autorevole"));
    }

    [Fact]
    public void RiportoPrimaSettimana_DiversoDallaFonteAutorevole_EBloccante()
    {
        var giornate = MeseCompleto(2026, 7, riportoPrimaSettimana: 16m);

        var problemi = Verifica(
            giornate, anno: 2026, mese: 7, orePregresse: 8m);

        Assert.Contains(problemi, p =>
            p.Bloccante && p.Messaggio.Contains("fonte autorevole ne indica 8"));
    }

    [Fact]
    public void MeseCheIniziaLunedi_NonRichiedeRiportoDalMesePrecedente()
    {
        var giornate = MeseCompleto(2026, 6);

        var problemi = Verifica(
            giornate, anno: 2026, mese: 6, orePregresse: null);

        Assert.DoesNotContain(problemi, p => p.Bloccante);
    }

    [Fact]
    public void SuperamentoSogliaSettimanalePrudenziale_EBloccante()
    {
        var giornate = MeseCompleto();
        int venerdi = giornate.FindIndex(g =>
            g.Data.DayOfWeek == DayOfWeek.Friday && g.Data.Day > 6);
        var g = giornate[venerdi] with { OreStraordinario = 9m };
        giornate[venerdi] = g;
        RicalcolaProgressivi(giornate);

        var problemi = Verifica(giornate);

        Assert.Contains(problemi, p =>
            p.Bloccante && p.Messaggio.Contains("soglia prudenziale"));
    }

    [Theory]
    [InlineData(CausaleGiornata.Malattia)]
    [InlineData(CausaleGiornata.Infortunio)]
    public void EventoSanitarioSenzaMetadati_EBloccante(CausaleGiornata causale)
    {
        var giornate = MeseCompleto();
        Sostituisci(giornate, 6, CausaleGiornata.Lavorata);
        giornate[5] = Giorno(6, causale) with { ProgressivoOreSettimanali = 0m };
        RicalcolaProgressivi(giornate);

        var problemi = Verifica(giornate);

        var problema = Assert.Single(problemi, p => p.Messaggio.Contains("ma mancano"));
        Assert.Contains("numero/protocollo", problema.Messaggio);
        Assert.Contains("RMG del mese precedente", problema.Messaggio);
        Assert.Contains("ratei giornalieri", problema.Messaggio);
        Assert.Contains("lordizzazione", problema.Messaggio);
    }

    [Theory]
    [InlineData("identificativo", "numero/protocollo")]
    [InlineData("inizio", "data di inizio")]
    [InlineData("rmg", "RMG del mese precedente")]
    [InlineData("ratei", "ratei giornalieri")]
    [InlineData("lordizzazione", "coefficiente di lordizzazione")]
    public void OgniMetadatoEvento_EObbligatorioAncheSeGliAltriSonoPresenti(
        string campo, string testoAtteso)
    {
        var giornate = MeseCompleto();
        var completo = Evento(6, CausaleGiornata.Malattia, 6);
        var incompleto = campo switch
        {
            "identificativo" => completo with { IdentificativoEvento = null },
            "inizio" => completo with { DataInizioEvento = null },
            "rmg" => completo with { RetribuzioneMediaGiornalieraPrecedente = null },
            "ratei" => completo with { RateiGiornalieri = null },
            _ => completo with { CoefficienteLordizzazione = null },
        };
        Sostituisci(giornate, 6, incompleto);

        var problemi = Verifica(giornate);

        Assert.Contains(problemi, p =>
            p.Bloccante && p.Messaggio.Contains(testoAtteso, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void EventoSanitarioInterrotto_EBloccante()
    {
        var giornate = MeseCompleto();
        Sostituisci(giornate, 8, Evento(8, CausaleGiornata.Malattia, 8));
        Sostituisci(giornate, 10, Evento(10, CausaleGiornata.Malattia, 8));

        var problemi = Verifica(giornate);

        Assert.Contains(problemi, p =>
            p.Bloccante && p.Messaggio.Contains("non è continuo"));
    }

    [Fact]
    public void PiuEventiNelMese_SonoBloccantiFincheIlRiepilogoERimastoSingolo()
    {
        var giornate = MeseCompleto();
        Sostituisci(giornate, 8, Evento(8, CausaleGiornata.Malattia, 8, "MAL-1"));
        Sostituisci(giornate, 9, Evento(9, CausaleGiornata.Malattia, 9, "MAL-2"));

        var problemi = Verifica(giornate);

        Assert.Contains(problemi, p =>
            p.Bloccante && p.Messaggio.Contains("più eventi sanitari"));
    }

    [Fact]
    public void MetadatiEventoSuGiornataOrdinaria_SonoBloccanti()
    {
        var giornate = MeseCompleto();
        giornate[0] = giornate[0] with { IdentificativoEvento = "NON-AMMESSO" };

        var problemi = Verifica(giornate);

        Assert.Contains(problemi, p =>
            p.Bloccante && p.Messaggio.Contains("non è malattia o infortunio"));
    }

    [Fact]
    public void EventoCompleto_VieneCopiatoNelRiepilogoMensile()
    {
        var giornate = MeseCompleto();
        foreach (int giorno in new[] { 8, 9, 10 })
            Sostituisci(giornate, giorno, Evento(giorno, CausaleGiornata.Malattia, 8));

        var cartellino = Compila(Mese2026(), giornate);

        Assert.Equal("EVT-2026-0001", cartellino.IdentificativoEvento);
        Assert.Equal(new DateOnly(Anno, Mese, 8), cartellino.DataInizioEvento);
        Assert.Equal(77.5058m, cartellino.RetribuzioneMediaGiornalieraPrecedente);
        Assert.Equal(6.25m, cartellino.RateiGiornalieri);
        Assert.Equal(1.1234m, cartellino.CoefficienteLordizzazione);
    }

    [Fact]
    public void ScenarioP0_ITotaliDerivanoDalCalendario()
    {
        var giornate = MeseCompleto();
        foreach (int giorno in new[] { 1, 2, 3, 6, 7 })
            Sostituisci(giornate, giorno, CausaleGiornata.Ferie);
        foreach (int giorno in new[] { 8, 9, 10, 11, 12 })
            Sostituisci(
                giornate, giorno, Evento(giorno, CausaleGiornata.Malattia, 8));
        foreach (int giorno in new[] { 13, 14 })
            AssenzaIntera(giornate, giorno);

        var cartellino = Compila(
            new Cartellino { Anno = Anno, Mese = Mese, NumeroEventoMalattiaAnno = 2 },
            giornate);

        Assert.Equal(5m, cartellino.GiorniFerie);
        Assert.Equal(5m, cartellino.GiorniMalattia);
        Assert.Equal(2, cartellino.NumeroEventoMalattiaAnno);
        Assert.Equal(16m, cartellino.OreAssenzaNonRetribuita);
        Assert.Equal(30, cartellino.Giornate.Count);
        Assert.Equal(12, cartellino.GiornateLavorate);
    }

    [Fact]
    public void PermessiEStraordinari_SiSommanoDalleGiornate()
    {
        var giornate = MeseCompleto();
        Sostituisci(giornate, 6, new GiornataCartellino
        {
            Data = new DateOnly(Anno, Mese, 6),
            Causale = CausaleGiornata.Lavorata,
            OreOrdinarie = 4m,
            OrePermessi = 4m,
        });
        Sostituisci(giornate, 7, new GiornataCartellino
        {
            Data = new DateOnly(Anno, Mese, 7),
            Causale = CausaleGiornata.PermessoRetribuito,
            OrePermessi = OreGiorno,
        });
        int indice = giornate.FindIndex(g => g.Data.Day == 8);
        giornate[indice] = giornate[indice] with { OreStraordinario = 2m };
        RicalcolaProgressivi(giornate);

        var cartellino = Compila(Mese2026(), giornate);

        Assert.Equal(12m, cartellino.OrePermessi);
        Assert.Equal(2m, cartellino.OreStraordinario);
    }

    [Fact]
    public void DatiDelMeseNonNatiDalCalendario_RestanoQuelliIndicati()
    {
        var cartellino = Compila(
            new Cartellino
            {
                Anno = Anno,
                Mese = Mese,
                GiorniBuonoPasto = 20,
                ValoreBuonoPasto = 8m,
                GiorniTrasfertaItalia = 3,
                IndennitaTrasfertaGiornaliera = 50m,
                PremioRisultato = 1200m,
                RiferimentoQuattordicesimaSeparata = "Q14-2026-07-001",
                DataPagamentoQuattordicesima = new DateOnly(2026, 7, 1),
            },
            MeseCompleto());

        Assert.Equal(20, cartellino.GiorniBuonoPasto);
        Assert.Equal(3m, cartellino.GiorniTrasfertaItalia);
        Assert.Equal(1200m, cartellino.PremioRisultato);
        Assert.Equal("Q14-2026-07-001", cartellino.RiferimentoQuattordicesimaSeparata);
        Assert.Equal(new DateOnly(2026, 7, 1), cartellino.DataPagamentoQuattordicesima);
    }

    [Fact]
    public void CalendarioConProblemiBloccanti_NonSiCompila()
    {
        var giornate = MeseCompleto();
        giornate.RemoveAll(g => g.Data.Day == 12);

        var errore = Assert.Throws<ArgumentException>(
            () => Compila(Mese2026(), giornate));

        Assert.Contains("non è elaborabile", errore.Message);
        Assert.Contains("12/04", errore.Message);
    }
}
