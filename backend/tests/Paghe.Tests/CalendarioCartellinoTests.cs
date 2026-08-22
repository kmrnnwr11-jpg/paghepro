using Paghe.Core.Domain;
using Xunit;

namespace Paghe.Tests;

/// <summary>
/// Calendario giornaliero del cartellino: la diagnostica che ferma i mesi mal compilati e la
/// derivazione dei totali che il motore legge. Include lo scenario di accettazione P0 scritto in
/// docs/RICERCA-CEDOLINO-PROFESSIONALE-2026.md.
/// </summary>
public sealed class CalendarioCartellinoTests
{
    private const decimal OreGiorno = 8m;
    private const int Anno = 2026;
    private const int Mese = 4; // aprile: 30 giorni

    private static Cartellino Mese2026() => new() { Anno = Anno, Mese = Mese };

    private static GiornataCartellino Giorno(
        int giorno, CausaleGiornata causale, decimal ore = 0m) => new()
    {
        Data = new DateOnly(Anno, Mese, giorno),
        Causale = causale,
        OreOrdinarie = ore,
    };

    /// <summary>Mese completo: lavorato nei feriali, riposo la domenica. Base dei casi buoni.</summary>
    private static List<GiornataCartellino> MeseCompleto()
    {
        var giornate = new List<GiornataCartellino>();
        for (int g = 1; g <= DateTime.DaysInMonth(Anno, Mese); g++)
        {
            var data = new DateOnly(Anno, Mese, g);
            giornate.Add(data.DayOfWeek == DayOfWeek.Sunday
                ? Giorno(g, CausaleGiornata.RiposoSettimanale)
                : Giorno(g, CausaleGiornata.Lavorata, OreGiorno));
        }
        return giornate;
    }

    private static void Sostituisci(
        List<GiornataCartellino> giornate, int giorno, CausaleGiornata causale, decimal ore = 0m)
    {
        int i = giornate.FindIndex(g => g.Data.Day == giorno);
        giornate[i] = Giorno(giorno, causale, ore);
    }

    /// <summary>Giornata intera di assenza non retribuita: porta le sue ore, come pretende la regola.</summary>
    private static void AssenzaIntera(List<GiornataCartellino> giornate, int giorno)
    {
        int i = giornate.FindIndex(g => g.Data.Day == giorno);
        giornate[i] = new GiornataCartellino
        {
            Data = new DateOnly(Anno, Mese, giorno),
            Causale = CausaleGiornata.AssenzaNonRetribuita,
            OreAssenzaNonRetribuita = OreGiorno,
        };
    }

    // ---- Diagnostica ----

    [Fact]
    public void GiorniScoperti_SonoBloccanti()
    {
        var giornate = MeseCompleto();
        giornate.RemoveAll(g => g.Data.Day is 7 or 8);

        var problemi = CalendarioCartellino.Verifica(Anno, Mese, giornate, OreGiorno);

        var problema = Assert.Single(problemi, p => p.Messaggio.Contains("Mancano"));
        Assert.True(problema.Bloccante);
        Assert.Contains("07/04", problema.Messaggio);
        Assert.Contains("08/04", problema.Messaggio);
    }

    [Fact]
    public void GiornoIndicatoDueVolte_EBloccante()
    {
        var giornate = MeseCompleto();
        giornate.Add(Giorno(10, CausaleGiornata.Ferie));

        var problemi = CalendarioCartellino.Verifica(Anno, Mese, giornate, OreGiorno);

        Assert.Contains(problemi, p => p.Bloccante && p.Messaggio.Contains("più di una volta"));
    }

    [Fact]
    public void CausaleNumericaSconosciuta_EBloccante()
    {
        var giornate = MeseCompleto();
        giornate[0] = new GiornataCartellino
        {
            Data = new DateOnly(Anno, Mese, 1),
            Causale = (CausaleGiornata)99,
        };

        var problemi = CalendarioCartellino.Verifica(Anno, Mese, giornate, OreGiorno);

        Assert.Contains(problemi, p =>
            p.Bloccante && p.Messaggio.Contains("non riconosciuta", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void GiornataDiUnAltroMese_EBloccante()
    {
        var giornate = MeseCompleto();
        giornate.Add(new GiornataCartellino
        {
            Data = new DateOnly(Anno, 5, 1), Causale = CausaleGiornata.Lavorata, OreOrdinarie = OreGiorno,
        });

        var problemi = CalendarioCartellino.Verifica(Anno, Mese, giornate, OreGiorno);

        Assert.Contains(problemi, p => p.Bloccante && p.Messaggio.Contains("non appartiene al mese"));
    }

    [Fact]
    public void GiornataLavorataSenzaOre_EBloccante()
    {
        var giornate = MeseCompleto();
        Sostituisci(giornate, 6, CausaleGiornata.Lavorata, ore: 0m);

        var problemi = CalendarioCartellino.Verifica(Anno, Mese, giornate, OreGiorno);

        Assert.Contains(problemi, p => p.Bloccante && p.Messaggio.Contains("non ha ore"));
    }

    [Fact]
    public void FerieConOreLavorate_EBloccante()
    {
        var giornate = MeseCompleto();
        Sostituisci(giornate, 6, CausaleGiornata.Ferie, ore: 8m);

        var problemi = CalendarioCartellino.Verifica(Anno, Mese, giornate, OreGiorno);

        Assert.Contains(problemi, p =>
            p.Bloccante && p.Messaggio.Contains("ferie") && p.Messaggio.Contains("ore lavorate"));
    }

    [Fact]
    public void MalattiaConStraordinario_EBloccante()
    {
        var giornate = MeseCompleto();
        int i = giornate.FindIndex(g => g.Data.Day == 6);
        giornate[i] = new GiornataCartellino
        {
            Data = new DateOnly(Anno, Mese, 6), Causale = CausaleGiornata.Malattia, OreStraordinario = 2m,
        };

        var problemi = CalendarioCartellino.Verifica(Anno, Mese, giornate, OreGiorno);

        Assert.Contains(problemi, p => p.Bloccante && p.Messaggio.Contains("non può avere straordinari"));
    }

    [Fact]
    public void OreOltreIlContrattuale_EUnAvvisoNonBloccante()
    {
        var giornate = MeseCompleto();
        Sostituisci(giornate, 6, CausaleGiornata.Lavorata, ore: 10m);

        var problemi = CalendarioCartellino.Verifica(Anno, Mese, giornate, OreGiorno);

        var avviso = Assert.Single(problemi, p => p.Messaggio.Contains("straordinario"));
        Assert.False(avviso.Bloccante);
    }

    [Fact]
    public void CartellinoATotali_SenzaCalendario_NonProduceProblemi() =>
        Assert.Empty(CalendarioCartellino.Verifica(Anno, Mese, [], OreGiorno));

    // ---- Derivazione dei totali ----

    /// <summary>
    /// Scenario di accettazione P0: 10 giorni lavorati, 5 di malattia collegati a un evento,
    /// 5 di ferie e 2 di assenza non retribuita, su date non sovrapposte.
    /// </summary>
    [Fact]
    public void ScenarioP0_ITotaliDerivanoDalCalendario()
    {
        var giornate = MeseCompleto();
        foreach (int g in new[] { 1, 2, 3, 6, 7 }) Sostituisci(giornate, g, CausaleGiornata.Ferie);
        foreach (int g in new[] { 8, 9, 10, 13, 14 }) Sostituisci(giornate, g, CausaleGiornata.Malattia);
        foreach (int g in new[] { 15, 16 }) AssenzaIntera(giornate, g);

        var cartellino = CalendarioCartellino.Compila(
            new Cartellino { Anno = Anno, Mese = Mese, NumeroEventoMalattiaAnno = 2 },
            giornate, OreGiorno);

        Assert.Equal(5m, cartellino.GiorniFerie);
        Assert.Equal(5m, cartellino.GiorniMalattia);
        Assert.Equal(2, cartellino.NumeroEventoMalattiaAnno);
        Assert.Equal(16m, cartellino.OreAssenzaNonRetribuita); // 2 giornate × 8 ore
        Assert.Equal(30, cartellino.Giornate.Count);           // il calendario resta allegato
        // Aprile 2026: 30 giorni, 4 domeniche di riposo, 12 giornate fra ferie/malattia/assenza.
        Assert.Equal(14, cartellino.GiornateLavorate);
    }

    [Fact]
    public void PermessiEStraordinari_SiSommanoDalleGiornate()
    {
        var giornate = MeseCompleto();
        int i = giornate.FindIndex(g => g.Data.Day == 6);
        giornate[i] = new GiornataCartellino
        {
            Data = new DateOnly(Anno, Mese, 6), Causale = CausaleGiornata.Lavorata,
            OreOrdinarie = 4m, OrePermessi = 4m, OreStraordinario = 2m,
        };
        int j = giornate.FindIndex(g => g.Data.Day == 7);
        giornate[j] = new GiornataCartellino
        {
            Data = new DateOnly(Anno, Mese, 7), Causale = CausaleGiornata.PermessoRetribuito,
            OrePermessi = OreGiorno,
        };

        var cartellino = CalendarioCartellino.Compila(Mese2026(), giornate, OreGiorno);

        Assert.Equal(12m, cartellino.OrePermessi);        // 4 ore + una giornata intera (8)
        Assert.Equal(2m, cartellino.OreStraordinario);
    }

    [Fact]
    public void CausaleAOre_SenzaOre_EBloccante()
    {
        // Il programma non decide da solo quanto vale una giornata di permesso: se le ore
        // mancano lo dice, invece di contare zero e produrre una busta silenziosamente sbagliata.
        var giornate = MeseCompleto();
        Sostituisci(giornate, 7, CausaleGiornata.PermessoRetribuito);

        var problemi = CalendarioCartellino.Verifica(Anno, Mese, giornate, OreGiorno);

        Assert.Contains(problemi, p => p.Bloccante && p.Messaggio.Contains("non indica quante ore"));
    }

    [Fact]
    public void DatiDelMeseNonNatiDalCalendario_RestanoQuelliIndicati()
    {
        var cartellino = CalendarioCartellino.Compila(
            new Cartellino
            {
                Anno = Anno, Mese = Mese,
                GiorniBuonoPasto = 20, ValoreBuonoPasto = 8m,
                GiorniTrasfertaItalia = 3, IndennitaTrasfertaGiornaliera = 50m,
                PremioRisultato = 1200m,
            },
            MeseCompleto(), OreGiorno);

        Assert.Equal(20, cartellino.GiorniBuonoPasto);
        Assert.Equal(3m, cartellino.GiorniTrasfertaItalia);
        Assert.Equal(1200m, cartellino.PremioRisultato);
    }

    [Fact]
    public void CalendarioConProblemiBloccanti_NonSiCompila()
    {
        var giornate = MeseCompleto();
        giornate.RemoveAll(g => g.Data.Day == 12);

        var errore = Assert.Throws<ArgumentException>(
            () => CalendarioCartellino.Compila(Mese2026(), giornate, OreGiorno));

        Assert.Contains("non è elaborabile", errore.Message);
        Assert.Contains("12/04", errore.Message);
    }
}
