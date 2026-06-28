namespace Paghe.Core.Calcolo;

/// <summary>
/// Scaglione del ravvedimento operoso: si applica fino a <see cref="GiorniMax"/> giorni di ritardo
/// (incluso). La sanzione è <see cref="Percentuale"/> del tributo; se <see cref="PerGiorno"/> è true
/// la percentuale si moltiplica per i giorni di ritardo (ravvedimento "sprint").
/// </summary>
public sealed record ScaglioneRavvedimento(
    string Nome,
    int GiorniMax,
    decimal Percentuale,
    bool PerGiorno = false);

/// <summary>Dettaglio del calcolo di ravvedimento: tributo, sanzione, interessi e totale da versare.</summary>
public sealed record RisultatoRavvedimento(
    int GiorniRitardo,
    string ScaglioneApplicato,
    decimal Tributo,
    decimal Sanzione,
    decimal Interessi,
    decimal Totale);

/// <summary>
/// Calcolo del <b>ravvedimento operoso</b> (art. 13 D.Lgs. 472/1997) per un versamento tardivo:
/// sanzione ridotta in base ai giorni di ritardo + interessi legali, da aggiungere al tributo.
///
/// <para>È un calcolatore <b>puro e parametrizzato</b>: le aliquote degli scaglioni e il tasso
/// legale sono <b>input</b>, non valori cablati. Gli scaglioni di esempio
/// (<see cref="ScaglioniEsempioStorico"/>) riflettono la tabella classica (sanzione base 30%) e
/// <b>vanno validati/aggiornati con un Consulente del Lavoro</b> — in particolare per le violazioni
/// dal 1° settembre 2024, soggette alla riforma sanzioni (sanzione base 25%).</para>
/// </summary>
public static class Ravvedimento
{
    /// <summary>
    /// Scaglioni di ESEMPIO (tabella storica, sanzione base 30%). Da validare/aggiornare: non sono
    /// dati certificati. Per le violazioni dal 1/9/2024 si applica la sanzione base ridotta (25%).
    /// </summary>
    public static IReadOnlyList<ScaglioneRavvedimento> ScaglioniEsempioStorico { get; } =
    [
        new("Sprint (entro 14 giorni)", 14, 0.001m, PerGiorno: true), // 0,1% per giorno
        new("Breve (entro 30 giorni)", 30, 0.015m),                   // 1,5%
        new("Trimestrale (entro 90 giorni)", 90, 0.0167m),            // ~1,67%
        new("Entro 1 anno", 365, 0.0375m),                            // 3,75%
        new("Entro 2 anni", 730, 0.0429m),                            // ~4,29%
        new("Oltre 2 anni", int.MaxValue, 0.05m),                     // 5%
    ];

    /// <summary>
    /// Calcola sanzione e interessi del ravvedimento. <paramref name="tassoLegaleAnnuo"/> è in forma
    /// decimale (es. 0,02 = 2%); gli interessi sono calcolati giorno/365 sul tributo. Lancia
    /// <see cref="ArgumentException"/> se non c'è ritardo o se gli scaglioni non coprono i giorni.
    /// </summary>
    public static RisultatoRavvedimento Calcola(
        decimal importoTributo,
        DateOnly scadenza,
        DateOnly versamento,
        decimal tassoLegaleAnnuo,
        IReadOnlyList<ScaglioneRavvedimento> scaglioni)
    {
        if (importoTributo <= 0)
            throw new ArgumentException("L'importo del tributo deve essere positivo.", nameof(importoTributo));
        if (scaglioni is null || scaglioni.Count == 0)
            throw new ArgumentException("Serve almeno uno scaglione di ravvedimento.", nameof(scaglioni));

        var giorni = versamento.DayNumber - scadenza.DayNumber;
        if (giorni <= 0)
            throw new ArgumentException("Nessun ritardo: la data di versamento non è successiva alla scadenza.");

        var scaglione = scaglioni
            .OrderBy(s => s.GiorniMax)
            .FirstOrDefault(s => giorni <= s.GiorniMax)
            ?? throw new ArgumentException("Nessuno scaglione copre i giorni di ritardo indicati.");

        var percentuale = scaglione.PerGiorno ? scaglione.Percentuale * giorni : scaglione.Percentuale;
        var sanzione = Arrotondamento.Euro(importoTributo * percentuale);
        var interessi = Arrotondamento.Euro(importoTributo * tassoLegaleAnnuo * giorni / 365m);
        var tributo = Arrotondamento.Euro(importoTributo);

        return new RisultatoRavvedimento(
            giorni, scaglione.Nome, tributo, sanzione, interessi, tributo + sanzione + interessi);
    }
}
