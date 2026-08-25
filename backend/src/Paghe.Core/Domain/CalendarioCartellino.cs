using System.Globalization;

namespace Paghe.Core.Domain;

/// <summary>Un problema trovato nel calendario del mese. Bloccante = non si elabora.</summary>
public sealed record ProblemaCartellino(string Messaggio, bool Bloccante);

/// <summary>
/// Controlli fail-closed del calendario e derivazione dei totali mensili. Le quantità ridondanti
/// non vengono accettate: giornata, causale, ore, progressivo settimanale ed evento devono
/// descrivere la stessa realtà prima che il cartellino possa alimentare un cedolino.
/// </summary>
public static class CalendarioCartellino
{
    /// <summary>Ore oltre le quali una giornata non è materialmente rappresentabile.</summary>
    private const decimal MassimoOreGiornaliere = 24m;

    /// <summary>
    /// Soglia preventiva predefinita. Non certifica la media sul periodo legale: il chiamante può
    /// sostituirla con il parametro versionato applicabile al rapporto.
    /// </summary>
    public const decimal LimiteOreSettimanaliPrudenzialePredefinito = 48m;

    private const int LunghezzaMassimaIdentificativoEvento = 100;

    /// <summary>
    /// Controlla completezza, esclusività delle causali, progressivi settimanali ed eventi.
    /// </summary>
    /// <param name="oreGiornaliereTeoriche">Ore contrattuali della giornata piena.</param>
    /// <param name="limiteOreSettimanaliPrudenziale">
    /// Soglia preventiva sulle ore effettivamente lavorate. Il superamento blocca e richiede la
    /// verifica del periodo di riferimento completo, che non è ricostruibile dal solo mese.
    /// </param>
    /// <param name="oreLavoratePregressePrimaSettimana">
    /// Ore autorevoli del mese precedente appartenenti alla prima settimana ISO. Sono richieste
    /// quando il mese non inizia di lunedì, così il riporto dichiarato non si auto-certifica.
    /// </param>
    public static IReadOnlyList<ProblemaCartellino> Verifica(
        int anno,
        int mese,
        IReadOnlyList<GiornataCartellino> giornate,
        decimal oreGiornaliereTeoriche,
        decimal limiteOreSettimanaliPrudenziale =
            LimiteOreSettimanaliPrudenzialePredefinito,
        decimal? oreLavoratePregressePrimaSettimana = null)
    {
        var problemi = new List<ProblemaCartellino>();

        if (anno is < 2000 or > 2100 || mese is < 1 or > 12)
        {
            problemi.Add(new ProblemaCartellino(
                $"Il periodo {mese:00}/{anno} non è valido.", true));
            return problemi;
        }

        if (oreGiornaliereTeoriche <= 0m)
            problemi.Add(new ProblemaCartellino(
                "Mancano le ore giornaliere teoriche del rapporto.", true));
        if (limiteOreSettimanaliPrudenziale <= 0m)
            problemi.Add(new ProblemaCartellino(
                "Manca il limite prudenziale delle ore settimanali.", true));

        if (giornate.Count == 0)
        {
            problemi.Add(new ProblemaCartellino(
                "Manca il calendario giornaliero completo: il cartellino a soli totali non è elaborabile.",
                true));
            return problemi;
        }

        int giorniDelMese = DateTime.DaysInMonth(anno, mese);
        var viste = new HashSet<DateOnly>();
        var valide = new List<GiornataCartellino>(giornate.Count);

        foreach (var g in giornate.OrderBy(g => g.Data))
        {
            string quando = g.Data.ToString("dd/MM", CultureInfo.InvariantCulture);

            if (!Enum.IsDefined(g.Causale))
            {
                problemi.Add(new ProblemaCartellino(
                    $"Il giorno {quando} ha una causale non riconosciuta.", true));
                continue;
            }

            if (g.Data.Year != anno || g.Data.Month != mese)
            {
                problemi.Add(new ProblemaCartellino(
                    $"La giornata del {g.Data:dd/MM/yyyy} non appartiene al mese {mese:00}/{anno}.",
                    true));
                continue;
            }

            if (!viste.Add(g.Data))
            {
                problemi.Add(new ProblemaCartellino(
                    $"Il giorno {quando} è indicato più di una volta.", true));
                continue;
            }

            valide.Add(g);

            if (QuantitaNegativa(g) is { } campoNegativo)
                problemi.Add(new ProblemaCartellino(
                    $"Il giorno {quando} ha un valore negativo ({campoNegativo}).", true));

            decimal totaleOre = g.OreOrdinarie + g.OrePermessi + g.OreExFestivita
                + g.OreAssenzaNonRetribuita + g.TotaleStraordinario;
            if (totaleOre > MassimoOreGiornaliere)
                problemi.Add(new ProblemaCartellino(
                    $"Il giorno {quando} somma {totaleOre:0.##} ore: più di quante ne abbia una giornata.",
                    true));

            VerificaCoerenzaGiornaliera(
                g, oreGiornaliereTeoriche, quando, problemi);
            VerificaMetadatiEventoGiornalieri(g, quando, problemi);
        }

        var scoperti = Enumerable.Range(1, giorniDelMese)
            .Select(giorno => new DateOnly(anno, mese, giorno))
            .Where(d => !viste.Contains(d))
            .ToList();
        if (scoperti.Count > 0)
            problemi.Add(new ProblemaCartellino(
                $"Mancano {scoperti.Count} giorni del mese: {Elenca(scoperti)}. "
                + "Ogni giorno va indicato, anche i riposi e le festività.", true));

        // Progressivi ed eventi si confrontano soltanto su un calendario univoco e completo;
        // diversamente il controllo produrrebbe diagnosi derivate da una base già incompleta.
        if (scoperti.Count == 0 && valide.Count == giorniDelMese)
        {
            VerificaProgressiviSettimanali(
                valide,
                limiteOreSettimanaliPrudenziale,
                oreLavoratePregressePrimaSettimana,
                problemi);
            VerificaSequenzaEventi(anno, mese, valide, problemi);
        }

        return problemi;
    }

    /// <summary>
    /// Costruisce il riepilogo mensile dal calendario dopo tutti i controlli bloccanti.
    /// </summary>
    /// <exception cref="ArgumentException">Il calendario non è completo o coerente.</exception>
    public static Cartellino Compila(
        Cartellino altriDati,
        IReadOnlyList<GiornataCartellino> giornate,
        decimal oreGiornaliereTeoriche,
        decimal limiteOreSettimanaliPrudenziale =
            LimiteOreSettimanaliPrudenzialePredefinito,
        decimal? oreLavoratePregressePrimaSettimana = null)
    {
        var problemi = Verifica(
            altriDati.Anno,
            altriDati.Mese,
            giornate,
            oreGiornaliereTeoriche,
            limiteOreSettimanaliPrudenziale,
            oreLavoratePregressePrimaSettimana);
        var bloccanti = problemi.Where(p => p.Bloccante).ToList();
        if (bloccanti.Count > 0)
            throw new ArgumentException(
                "Il calendario del mese non è elaborabile: "
                + string.Join(" ", bloccanti.Select(p => p.Messaggio)));

        decimal Conta(CausaleGiornata causale) =>
            giornate.Count(g => g.Causale == causale);
        var evento = giornate.FirstOrDefault(ÈEventoSanitario);

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
            IdentificativoEvento = evento?.IdentificativoEvento?.Trim(),
            DataInizioEvento = evento?.DataInizioEvento,
            RetribuzioneMediaGiornalieraPrecedente =
                evento?.RetribuzioneMediaGiornalieraPrecedente,
            RateiGiornalieri = evento?.RateiGiornalieri,
            CoefficienteLordizzazione = evento?.CoefficienteLordizzazione,
            OreAssenzaNonRetribuita = giornate.Sum(g => g.OreAssenzaNonRetribuita),

            StatoAssegniFamiliari = altriDati.StatoAssegniFamiliari,
            ImportoAssegniFamiliari = altriDati.ImportoAssegniFamiliari,

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

            RiferimentoQuattordicesimaSeparata = altriDati.RiferimentoQuattordicesimaSeparata,
            DataPagamentoQuattordicesima = altriDati.DataPagamentoQuattordicesima,

            Giornate = [.. giornate.OrderBy(g => g.Data)],
        };
    }

    private static void VerificaCoerenzaGiornaliera(
        GiornataCartellino g,
        decimal oreGiornaliereTeoriche,
        string quando,
        ICollection<ProblemaCartellino> problemi)
    {
        bool oreOrdinarie = g.OreOrdinarie != 0m;
        bool rol = g.OrePermessi != 0m;
        bool exFestivita = g.OreExFestivita != 0m;
        bool assenza = g.OreAssenzaNonRetribuita != 0m;
        bool straordinario = g.TotaleStraordinario != 0m;

        switch (g.Causale)
        {
            case CausaleGiornata.Lavorata:
                if (g.OreOrdinarie <= 0m)
                    problemi.Add(new ProblemaCartellino(
                        $"Il giorno {quando} è segnato come lavorato ma non indica ore lavorate.",
                        true));
                if (exFestivita || assenza)
                    problemi.Add(Incompatibile(quando, g.Causale));
                if (rol && straordinario)
                    problemi.Add(new ProblemaCartellino(
                        $"Il giorno {quando} non può cumulare ROL e straordinario.", true));
                if (oreGiornaliereTeoriche > 0m
                    && g.OreOrdinarie + g.OrePermessi != oreGiornaliereTeoriche)
                    problemi.Add(new ProblemaCartellino(
                        $"Il giorno {quando} deve totalizzare {oreGiornaliereTeoriche:0.##} ore "
                        + "fra lavoro ordinario e ROL; il totale indicato non coincide.", true));
                break;

            case CausaleGiornata.PermessoRetribuito:
                if (oreOrdinarie || exFestivita || assenza || straordinario)
                    problemi.Add(Incompatibile(quando, g.Causale));
                VerificaQuantitaGiornataIntera(
                    quando, "ROL", g.OrePermessi, oreGiornaliereTeoriche, problemi);
                break;

            case CausaleGiornata.ExFestivita:
                if (oreOrdinarie || rol || assenza || straordinario)
                    problemi.Add(Incompatibile(quando, g.Causale));
                VerificaQuantitaGiornataIntera(
                    quando, "ex festività", g.OreExFestivita,
                    oreGiornaliereTeoriche, problemi);
                break;

            case CausaleGiornata.AssenzaNonRetribuita:
                if (oreOrdinarie || rol || exFestivita || straordinario)
                    problemi.Add(Incompatibile(quando, g.Causale));
                VerificaQuantitaGiornataIntera(
                    quando, "assenza non retribuita", g.OreAssenzaNonRetribuita,
                    oreGiornaliereTeoriche, problemi);
                break;

            case CausaleGiornata.Ferie:
            case CausaleGiornata.Malattia:
            case CausaleGiornata.Infortunio:
            case CausaleGiornata.RiposoSettimanale:
            case CausaleGiornata.Festivita:
                if (oreOrdinarie || rol || exFestivita || assenza || straordinario)
                    problemi.Add(Incompatibile(quando, g.Causale));
                break;
        }
    }

    private static void VerificaQuantitaGiornataIntera(
        string quando,
        string descrizione,
        decimal ore,
        decimal oreGiornaliereTeoriche,
        ICollection<ProblemaCartellino> problemi)
    {
        if (ore <= 0m)
            problemi.Add(new ProblemaCartellino(
                $"Il giorno {quando} è {descrizione} ma non indica quante ore.", true));
        else if (oreGiornaliereTeoriche > 0m && ore != oreGiornaliereTeoriche)
            problemi.Add(new ProblemaCartellino(
                $"Il giorno {quando} è {descrizione} per l'intera giornata: deve indicare "
                + $"{oreGiornaliereTeoriche:0.##} ore, non {ore:0.##}.", true));
    }

    private static ProblemaCartellino Incompatibile(
        string quando, CausaleGiornata causale) => new(
        $"Il giorno {quando} è {Descrivi(causale)} ma contiene ore incompatibili con la causale.",
        true);

    private static void VerificaMetadatiEventoGiornalieri(
        GiornataCartellino g,
        string quando,
        ICollection<ProblemaCartellino> problemi)
    {
        if (!ÈEventoSanitario(g))
        {
            if (HaMetadatiEvento(g))
                problemi.Add(new ProblemaCartellino(
                    $"Il giorno {quando} non è malattia o infortunio ma contiene dati di un evento sanitario.",
                    true));
            return;
        }

        var mancanti = new List<string>();
        if (string.IsNullOrWhiteSpace(g.IdentificativoEvento))
            mancanti.Add("numero/protocollo");
        if (g.DataInizioEvento is null)
            mancanti.Add("data di inizio");
        if (g.RetribuzioneMediaGiornalieraPrecedente is null)
            mancanti.Add("RMG del mese precedente");
        if (g.RateiGiornalieri is null)
            mancanti.Add("ratei giornalieri");
        if (g.CoefficienteLordizzazione is null)
            mancanti.Add("coefficiente di lordizzazione");
        if (mancanti.Count > 0)
            problemi.Add(new ProblemaCartellino(
                $"Il giorno {quando} è {Descrivi(g.Causale)} ma mancano: "
                + string.Join(", ", mancanti) + ".", true));

        if (g.IdentificativoEvento?.Trim().Length > LunghezzaMassimaIdentificativoEvento)
            problemi.Add(new ProblemaCartellino(
                $"Il numero/protocollo dell'evento del {quando} supera "
                + $"{LunghezzaMassimaIdentificativoEvento} caratteri.", true));
        if (g.DataInizioEvento is { } inizio && inizio > g.Data)
            problemi.Add(new ProblemaCartellino(
                $"L'evento del {quando} risulta iniziato dopo la giornata indicata.", true));
        if (g.RetribuzioneMediaGiornalieraPrecedente is <= 0m)
            problemi.Add(new ProblemaCartellino(
                $"La RMG precedente dell'evento del {quando} deve essere maggiore di zero.", true));
        if (g.RateiGiornalieri is < 0m)
            problemi.Add(new ProblemaCartellino(
                $"I ratei giornalieri dell'evento del {quando} non possono essere negativi.", true));
        if (g.CoefficienteLordizzazione is <= 0m)
            problemi.Add(new ProblemaCartellino(
                $"Il coefficiente di lordizzazione dell'evento del {quando} deve essere maggiore di zero.",
                true));
    }

    private static void VerificaProgressiviSettimanali(
        IReadOnlyList<GiornataCartellino> giornate,
        decimal limite,
        decimal? oreLavoratePregressePrimaSettimana,
        ICollection<ProblemaCartellino> problemi)
    {
        bool primaSettimanaDelMese = true;
        foreach (var settimana in giornate
            .OrderBy(g => g.Data)
            .GroupBy(ChiaveSettimana))
        {
            decimal? precedente = null;
            bool sogliaSegnalata = false;

            foreach (var g in settimana.OrderBy(g => g.Data))
            {
                string quando = g.Data.ToString("dd/MM", CultureInfo.InvariantCulture);
                if (g.ProgressivoOreSettimanali is null)
                {
                    problemi.Add(new ProblemaCartellino(
                        $"Il giorno {quando} non riporta il progressivo delle ore settimanali.",
                        true));
                    continue;
                }

                decimal progressivo = g.ProgressivoOreSettimanali.Value;
                if (progressivo < 0m)
                {
                    problemi.Add(new ProblemaCartellino(
                        $"Il progressivo settimanale del {quando} non può essere negativo.", true));
                    precedente = progressivo;
                    continue;
                }

                if (precedente is { } progressivoPrecedente)
                {
                    decimal atteso = progressivoPrecedente + g.TotaleOreLavorate;
                    if (progressivo != atteso)
                        problemi.Add(new ProblemaCartellino(
                            $"Il progressivo settimanale del {quando} è {progressivo:0.##} ore, "
                            + $"ma dalle giornate risulta {atteso:0.##}.", true));
                }
                else
                {
                    decimal riporto = progressivo - g.TotaleOreLavorate;
                    decimal? riportoAtteso = g.Data.DayOfWeek == DayOfWeek.Monday
                        ? 0m
                        : primaSettimanaDelMese
                            ? oreLavoratePregressePrimaSettimana
                            : 0m;
                    if (riportoAtteso is null)
                        problemi.Add(new ProblemaCartellino(
                            $"Manca il riporto autorevole delle ore lavorate prima del "
                            + $"{g.Data:dd/MM/yyyy} nella stessa settimana ISO.", true));
                    else if (riportoAtteso < 0m)
                        problemi.Add(new ProblemaCartellino(
                            "Il riporto autorevole della prima settimana non può essere negativo.",
                            true));
                    else if (riporto != riportoAtteso.Value)
                        problemi.Add(new ProblemaCartellino(
                            $"Il progressivo settimanale del {quando} riporta {riporto:0.##} "
                            + $"ore pregresse, ma la fonte autorevole ne indica "
                            + $"{riportoAtteso.Value:0.##}.", true));
                }

                if (!sogliaSegnalata && limite > 0m && progressivo > limite)
                {
                    problemi.Add(new ProblemaCartellino(
                        $"La settimana ISO {settimana.Key.Settimana:00}/{settimana.Key.Anno} "
                        + $"raggiunge {progressivo:0.##} ore, oltre la soglia prudenziale di "
                        + $"{limite:0.##}: serve verificare il periodo medio completo.", true));
                    sogliaSegnalata = true;
                }

                precedente = progressivo;
            }

            primaSettimanaDelMese = false;
        }
    }

    private static void VerificaSequenzaEventi(
        int anno,
        int mese,
        IReadOnlyList<GiornataCartellino> giornate,
        ICollection<ProblemaCartellino> problemi)
    {
        var sanitarie = giornate.Where(ÈEventoSanitario).OrderBy(g => g.Data).ToList();
        if (sanitarie.Count == 0) return;

        var complete = sanitarie
            .Where(g => !string.IsNullOrWhiteSpace(g.IdentificativoEvento)
                && g.DataInizioEvento is not null
                && g.RetribuzioneMediaGiornalieraPrecedente is not null
                && g.RateiGiornalieri is not null
                && g.CoefficienteLordizzazione is not null)
            .ToList();
        if (complete.Count != sanitarie.Count) return;

        var eventi = complete.GroupBy(
            g => g.IdentificativoEvento!.Trim(), StringComparer.Ordinal).ToList();
        if (eventi.Count > 1)
            problemi.Add(new ProblemaCartellino(
                "Il cartellino mensile contiene più eventi sanitari: il riepilogo corrente può "
                + "rappresentarne uno solo e non deve scegliere quale usare.", true));

        var perData = giornate.ToDictionary(g => g.Data);
        var primoGiornoMese = new DateOnly(anno, mese, 1);

        foreach (var gruppo in eventi)
        {
            var righe = gruppo.OrderBy(g => g.Data).ToList();
            var prima = righe[0];
            bool coerente = righe.All(g =>
                g.Causale == prima.Causale
                && g.DataInizioEvento == prima.DataInizioEvento
                && g.RetribuzioneMediaGiornalieraPrecedente
                    == prima.RetribuzioneMediaGiornalieraPrecedente
                && g.RateiGiornalieri == prima.RateiGiornalieri
                && g.CoefficienteLordizzazione == prima.CoefficienteLordizzazione);
            if (!coerente)
            {
                problemi.Add(new ProblemaCartellino(
                    $"L'evento '{gruppo.Key}' cambia causale o dati di calcolo tra una giornata e l'altra.",
                    true));
                continue;
            }

            var inizio = prima.DataInizioEvento!.Value;
            var attesa = inizio < primoGiornoMese ? primoGiornoMese : inizio;
            var ultima = righe[^1].Data;
            for (var data = attesa; data <= ultima; data = data.AddDays(1))
            {
                if (!perData.TryGetValue(data, out var giornata)
                    || giornata.Causale != prima.Causale
                    || !string.Equals(
                        giornata.IdentificativoEvento?.Trim(), gruppo.Key,
                        StringComparison.Ordinal))
                {
                    problemi.Add(new ProblemaCartellino(
                        $"L'evento '{gruppo.Key}' iniziato il {inizio:dd/MM/yyyy} non è continuo: "
                        + $"il {data:dd/MM/yyyy} ha una causale o un identificativo diverso.", true));
                    break;
                }
            }
        }
    }

    private static (int Anno, int Settimana) ChiaveSettimana(GiornataCartellino g)
    {
        var data = g.Data.ToDateTime(TimeOnly.MinValue);
        return (ISOWeek.GetYear(data), ISOWeek.GetWeekOfYear(data));
    }

    private static bool ÈEventoSanitario(GiornataCartellino g) =>
        g.Causale is CausaleGiornata.Malattia or CausaleGiornata.Infortunio;

    private static bool HaMetadatiEvento(GiornataCartellino g) =>
        g.IdentificativoEvento is not null
        || g.DataInizioEvento is not null
        || g.RetribuzioneMediaGiornalieraPrecedente is not null
        || g.RateiGiornalieri is not null
        || g.CoefficienteLordizzazione is not null;

    private static string? QuantitaNegativa(GiornataCartellino g) =>
        g.OreOrdinarie < 0m ? "ore ordinarie"
        : g.OrePermessi < 0m ? "ROL"
        : g.OreExFestivita < 0m ? "ex festività"
        : g.OreAssenzaNonRetribuita < 0m ? "assenza non retribuita"
        : g.OreStraordinario < 0m
            || g.OreStraordinarioNotturno < 0m
            || g.OreStraordinarioFestivo < 0m ? "straordinario"
        : null;

    private static string Descrivi(CausaleGiornata causale) => causale switch
    {
        CausaleGiornata.Ferie => "ferie",
        CausaleGiornata.Malattia => "malattia",
        CausaleGiornata.Infortunio => "infortunio",
        CausaleGiornata.PermessoRetribuito => "ROL",
        CausaleGiornata.ExFestivita => "ex festività",
        CausaleGiornata.AssenzaNonRetribuita => "assenza non retribuita",
        CausaleGiornata.RiposoSettimanale => "riposo settimanale",
        CausaleGiornata.Festivita => "festività",
        _ => "lavorata",
    };

    private static string Elenca(IReadOnlyList<DateOnly> giorni) =>
        string.Join(", ", giorni.Take(6).Select(d =>
            d.ToString("dd/MM", CultureInfo.InvariantCulture)))
        + (giorni.Count > 6 ? $" e altri {giorni.Count - 6}" : "");
}
