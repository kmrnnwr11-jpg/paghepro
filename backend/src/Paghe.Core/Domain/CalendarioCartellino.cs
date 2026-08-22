namespace Paghe.Core.Domain;

/// <summary>Un problema trovato nel calendario del mese. Bloccante = non si elabora.</summary>
public sealed record ProblemaCartellino(string Messaggio, bool Bloccante);

/// <summary>
/// Il calendario giorno per giorno: controlli prima dell'elaborazione e derivazione dei totali
/// mensili che il motore già sa leggere. Il cartellino a totali resta valido — chi non compila
/// il calendario continua a lavorare come prima — ma quando il calendario c'è è lui la verità:
/// i totali vengono ricalcolati da qui, così cedolino, LUL e denunce raccontano lo stesso mese.
/// </summary>
public static class CalendarioCartellino
{
    /// <summary>Ore oltre le quali una giornata non è credibile: serve a fermare i refusi.</summary>
    private const decimal MassimoOreGiornaliere = 24m;

    /// <summary>
    /// Controlla il calendario del mese. Restituisce l'elenco dei problemi: quelli bloccanti
    /// impediscono l'elaborazione, gli altri sono avvisi da mostrare a chi compila.
    /// </summary>
    /// <param name="oreGiornaliereTeoriche">
    /// Ore contrattuali della giornata piena (es. 8 con 40 ore settimanali su 5 giorni, 6,5 in
    /// agricoltura): serve a segnalare le eccedenze non marcate come straordinario.
    /// </param>
    public static IReadOnlyList<ProblemaCartellino> Verifica(
        int anno, int mese, IReadOnlyList<GiornataCartellino> giornate,
        decimal oreGiornaliereTeoriche)
    {
        var problemi = new List<ProblemaCartellino>();
        if (giornate.Count == 0) return problemi;

        int giorniDelMese = DateTime.DaysInMonth(anno, mese);
        var viste = new HashSet<DateOnly>();

        foreach (var g in giornate.OrderBy(g => g.Data))
        {
            string quando = g.Data.ToString("dd/MM");

            if (!Enum.IsDefined(g.Causale))
            {
                problemi.Add(new ProblemaCartellino(
                    $"Il giorno {quando} ha una causale non riconosciuta.", true));
                continue;
            }

            if (g.Data.Year != anno || g.Data.Month != mese)
            {
                problemi.Add(new ProblemaCartellino(
                    $"La giornata del {g.Data:dd/MM/yyyy} non appartiene al mese {mese:00}/{anno}.", true));
                continue;
            }

            if (!viste.Add(g.Data))
            {
                problemi.Add(new ProblemaCartellino(
                    $"Il giorno {quando} è indicato più di una volta.", true));
                continue;
            }

            if (Negative(g) is { } campoNegativo)
                problemi.Add(new ProblemaCartellino(
                    $"Il giorno {quando} ha ore negative ({campoNegativo}).", true));

            decimal totaleOre = g.OreOrdinarie + g.OrePermessi + g.OreExFestivita
                + g.OreAssenzaNonRetribuita + g.TotaleStraordinario;
            if (totaleOre > MassimoOreGiornaliere)
                problemi.Add(new ProblemaCartellino(
                    $"Il giorno {quando} somma {totaleOre:0.##} ore: più di quante ne abbia una giornata.", true));

            if (g.Causale == CausaleGiornata.Lavorata && g.OreOrdinarie <= 0m
                && g.OrePermessi <= 0m && g.OreExFestivita <= 0m)
                problemi.Add(new ProblemaCartellino(
                    $"Il giorno {quando} è segnato come lavorato ma non ha ore: indica le ore o cambia causale.", true));

            if (g.Causale != CausaleGiornata.Lavorata && g.OreOrdinarie > 0m)
                problemi.Add(new ProblemaCartellino(
                    $"Il giorno {quando} è {Descrivi(g.Causale)} ma riporta ore lavorate: le due cose non stanno insieme.", true));

            if (g.Causale is CausaleGiornata.Malattia or CausaleGiornata.Infortunio
                && g.TotaleStraordinario > 0m)
                problemi.Add(new ProblemaCartellino(
                    $"Il giorno {quando} è {Descrivi(g.Causale)} e non può avere straordinari.", true));

            // Le causali che si pagano a ore devono portarsele: il programma non inventa
            // quante ore vale una giornata, perché da quel numero dipende la trattenuta.
            if (OreMancanti(g))
                problemi.Add(new ProblemaCartellino(
                    $"Il giorno {quando} è {Descrivi(g.Causale)} ma non indica quante ore: "
                    + "senza, la giornata non conta nulla in busta.", true));

            if (oreGiornaliereTeoriche > 0m && g.OreOrdinarie > oreGiornaliereTeoriche)
                problemi.Add(new ProblemaCartellino(
                    $"Il giorno {quando} ha {g.OreOrdinarie:0.##} ore ordinarie contro le "
                    + $"{oreGiornaliereTeoriche:0.##} contrattuali: l'eccedenza andrebbe segnata come straordinario.",
                    false));
        }

        var scoperti = Enumerable.Range(1, giorniDelMese)
            .Select(giorno => new DateOnly(anno, mese, giorno))
            .Where(d => !viste.Contains(d))
            .ToList();
        if (scoperti.Count > 0)
            problemi.Add(new ProblemaCartellino(
                $"Mancano {scoperti.Count} giorni del mese: {Elenca(scoperti)}. "
                + "Ogni giorno va indicato, anche i riposi e le festività.", true));

        return problemi;
    }

    /// <summary>
    /// Costruisce il cartellino del mese dal calendario: i totali che il motore legge vengono
    /// derivati dalle giornate, gli altri dati del mese (trasferte, buoni pasto, premio, numero
    /// dell'evento di malattia) sono ripresi da <paramref name="altriDati"/>.
    /// </summary>
    /// <exception cref="ArgumentException">Il calendario ha problemi bloccanti.</exception>
    public static Cartellino Compila(
        Cartellino altriDati, IReadOnlyList<GiornataCartellino> giornate,
        decimal oreGiornaliereTeoriche)
    {
        var problemi = Verifica(altriDati.Anno, altriDati.Mese, giornate, oreGiornaliereTeoriche);
        var bloccanti = problemi.Where(p => p.Bloccante).ToList();
        if (bloccanti.Count > 0)
            throw new ArgumentException(
                "Il calendario del mese non è elaborabile: "
                + string.Join(" ", bloccanti.Select(p => p.Messaggio)));

        decimal Conta(CausaleGiornata causale) => giornate.Count(g => g.Causale == causale);

        return new Cartellino
        {
            Anno = altriDati.Anno,
            Mese = altriDati.Mese,

            OreStraordinario = giornate.Sum(g => g.OreStraordinario),
            OreStraordinarioNotturno = giornate.Sum(g => g.OreStraordinarioNotturno),
            OreStraordinarioFestivo = giornate.Sum(g => g.OreStraordinarioFestivo),

            GiorniFerie = Conta(CausaleGiornata.Ferie),
            OrePermessi = giornate.Sum(g => g.OrePermessi),
            OreExFestivita = giornate.Sum(g => g.OreExFestivita),

            GiorniMalattia = Conta(CausaleGiornata.Malattia),
            NumeroEventoMalattiaAnno = altriDati.NumeroEventoMalattiaAnno,
            GiorniInfortunio = Conta(CausaleGiornata.Infortunio),
            OreAssenzaNonRetribuita = giornate.Sum(g => g.OreAssenzaNonRetribuita),

            StatoAssegniFamiliari = altriDati.StatoAssegniFamiliari,
            ImportoAssegniFamiliari = altriDati.ImportoAssegniFamiliari,

            // Dati del mese che non nascono dal calendario: restano come li ha indicati l'utente.
            GiorniTrasfertaItalia = altriDati.GiorniTrasfertaItalia,
            GiorniTrasfertaEstero = altriDati.GiorniTrasfertaEstero,
            IndennitaTrasfertaGiornaliera = altriDati.IndennitaTrasfertaGiornaliera,
            RimborsoTrasferta = altriDati.RimborsoTrasferta,
            RimborsiSpeseDocumentate = altriDati.RimborsiSpeseDocumentate,
            FringeBenefit = altriDati.FringeBenefit,
            FringeBenefitGiaErogatoAnno = altriDati.FringeBenefitGiaErogatoAnno,
            FigliACarico = altriDati.FigliACarico,
            GiorniBuonoPasto = altriDati.GiorniBuonoPasto,
            ValoreBuonoPasto = altriDati.ValoreBuonoPasto,
            BuonoPastoElettronico = altriDati.BuonoPastoElettronico,
            PremioRisultato = altriDati.PremioRisultato,
            RedditoAnnoPrecedente = altriDati.RedditoAnnoPrecedente,

            Giornate = [.. giornate.OrderBy(g => g.Data)],
        };
    }

    /// <summary>La causale si paga a ore ma la giornata non ne porta nessuna.</summary>
    private static bool OreMancanti(GiornataCartellino g) => g.Causale switch
    {
        CausaleGiornata.PermessoRetribuito => g.OrePermessi <= 0m,
        CausaleGiornata.ExFestivita => g.OreExFestivita <= 0m,
        CausaleGiornata.AssenzaNonRetribuita => g.OreAssenzaNonRetribuita <= 0m,
        _ => false,
    };

    /// <summary>Nome del primo campo con un valore negativo, null se sono tutti a posto.</summary>
    private static string? Negative(GiornataCartellino g) =>
        g.OreOrdinarie < 0m ? "ore ordinarie"
        : g.OrePermessi < 0m ? "permessi"
        : g.OreExFestivita < 0m ? "ex festività"
        : g.OreAssenzaNonRetribuita < 0m ? "assenza non retribuita"
        : g.OreStraordinario < 0m || g.OreStraordinarioNotturno < 0m || g.OreStraordinarioFestivo < 0m
            ? "straordinario"
        : null;

    private static string Descrivi(CausaleGiornata causale) => causale switch
    {
        CausaleGiornata.Ferie => "ferie",
        CausaleGiornata.Malattia => "malattia",
        CausaleGiornata.Infortunio => "infortunio",
        CausaleGiornata.PermessoRetribuito => "permesso retribuito",
        CausaleGiornata.ExFestivita => "ex festività",
        CausaleGiornata.AssenzaNonRetribuita => "assenza non retribuita",
        CausaleGiornata.RiposoSettimanale => "riposo settimanale",
        CausaleGiornata.Festivita => "festività",
        _ => "lavorata",
    };

    /// <summary>Elenco compatto di date: i primi giorni per esteso, poi "e altri N".</summary>
    private static string Elenca(IReadOnlyList<DateOnly> giorni) =>
        string.Join(", ", giorni.Take(6).Select(d => d.ToString("dd/MM")))
        + (giorni.Count > 6 ? $" e altri {giorni.Count - 6}" : "");
}
