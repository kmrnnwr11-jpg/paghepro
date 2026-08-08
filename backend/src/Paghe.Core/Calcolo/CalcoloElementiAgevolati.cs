using Paghe.Core.Domain;
using Paghe.Core.Parametri;

namespace Paghe.Core.Calcolo;

/// <summary>
/// Ripartizione di un elemento retributivo tra quota esente e quota imponibile.
/// </summary>
public readonly record struct QuotaAgevolata(decimal Esente, decimal Imponibile)
{
    public decimal Totale => Esente + Imponibile;
    public static QuotaAgevolata Nessuna => new(0m, 0m);
}

/// <summary>
/// Elementi retributivi con trattamento fiscale agevolato: trasferta, rimborsi documentati,
/// fringe benefit, buoni pasto e premio di risultato.
///
/// Regola generale: l'esenzione spetta solo se l'elemento è realmente dovuto. Il software
/// calcola i limiti, non attesta i presupposti — la prova della trasferta (ordine di servizio,
/// destinazione, date, giustificativi tracciabili) resta un obbligo documentale del datore.
/// </summary>
public static class CalcoloElementiAgevolati
{
    /// <summary>
    /// Indennità forfettaria di trasferta. Esente entro il limite giornaliero; l'eccedenza è
    /// imponibile. Con il rimborso misto la soglia si riduce di un terzo (rimborsata una fra
    /// vitto e alloggio) o di due terzi (rimborsate entrambe).
    /// </summary>
    public static QuotaAgevolata Trasferta(
        decimal giorni, decimal indennitaGiornaliera, decimal limiteGiorno,
        TipoRimborsoTrasferta tipo)
    {
        if (giorni <= 0m || indennitaGiornaliera <= 0m) return QuotaAgevolata.Nessuna;

        decimal riduzione = tipo switch
        {
            TipoRimborsoTrasferta.MistoVittoOAlloggio => 1m / 3m,
            TipoRimborsoTrasferta.MistoVittoEAlloggio => 2m / 3m,
            _ => 0m,
        };
        decimal limiteEffettivo = limiteGiorno * (1m - riduzione);

        decimal esenteGiorno = Math.Min(indennitaGiornaliera, limiteEffettivo);
        decimal esente = Arrotondamento.Euro(giorni * esenteGiorno);
        decimal totale = Arrotondamento.Euro(giorni * indennitaGiornaliera);
        return new QuotaAgevolata(esente, totale - esente);
    }

    /// <summary>
    /// Fringe benefit: soglia ANNUA con la regola «tutto o niente». Superata di un solo euro,
    /// diventa imponibile l'intero importo dell'anno, non la sola eccedenza. Per questo il
    /// calcolo considera anche quanto già erogato nei mesi precedenti.
    /// </summary>
    public static QuotaAgevolata FringeBenefit(
        decimal importoMese, decimal giaErogatoAnno, decimal soglia)
    {
        if (importoMese <= 0m) return QuotaAgevolata.Nessuna;

        decimal totaleAnno = giaErogatoAnno + importoMese;
        return totaleAnno > soglia
            ? new QuotaAgevolata(0m, Arrotondamento.Euro(importoMese))   // tutto o niente
            : new QuotaAgevolata(Arrotondamento.Euro(importoMese), 0m);
    }

    /// <summary>
    /// Buoni pasto: limite GIORNALIERO e, a differenza dei fringe benefit, è imponibile la sola
    /// eccedenza. Non concorrono alla soglia dei fringe benefit.
    /// </summary>
    public static QuotaAgevolata BuoniPasto(
        int giorni, decimal valoreBuono, decimal limiteGiorno)
    {
        if (giorni <= 0 || valoreBuono <= 0m) return QuotaAgevolata.Nessuna;

        decimal esenteGiorno = Math.Min(valoreBuono, limiteGiorno);
        decimal eccedenzaGiorno = Math.Max(0m, valoreBuono - limiteGiorno);
        return new QuotaAgevolata(
            Arrotondamento.Euro(giorni * esenteGiorno),
            Arrotondamento.Euro(giorni * eccedenzaGiorno));
    }

    /// <summary>
    /// Premio di risultato: quota agevolabile entro il limite annuo, se il reddito dell'anno
    /// precedente non supera il tetto. La quota agevolata resta soggetta a contribuzione INPS:
    /// è sostituita la sola IRPEF. L'eccedenza segue la tassazione ordinaria.
    /// </summary>
    /// <returns>Quota agevolata (imposta sostitutiva) e quota a tassazione ordinaria.</returns>
    public static (decimal Agevolato, decimal Ordinario) PremioRisultato(
        decimal premio, decimal redditoAnnoPrecedente, decimal limiteAnnuo, decimal redditoMassimo)
    {
        if (premio <= 0m) return (0m, 0m);

        // Fuori dai requisiti soggettivi: nessuna agevolazione, tassazione piena.
        if (redditoAnnoPrecedente > redditoMassimo)
            return (0m, Arrotondamento.Euro(premio));

        decimal agevolato = Math.Min(premio, limiteAnnuo);
        return (Arrotondamento.Euro(agevolato), Arrotondamento.Euro(premio - agevolato));
    }

    /// <summary>Soglia annua di esenzione dei fringe benefit applicabile al lavoratore.</summary>
    public static decimal SogliaFringeBenefit(ParametriNormativi p, bool figliACarico) =>
        figliACarico ? p.FringeBenefitSogliaAnnuaConFigli : p.FringeBenefitSogliaAnnua;
}
