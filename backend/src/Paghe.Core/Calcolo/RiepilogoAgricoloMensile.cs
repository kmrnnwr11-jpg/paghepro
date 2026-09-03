using System.Globalization;

namespace Paghe.Core.Calcolo;

/// <summary>Riga del riepilogo mensile aziendale delle buste agricole (un lavoratore).</summary>
public sealed record RigaRiepilogoAgricolo(
    string Nominativo,
    decimal Ore,
    int Giornate,
    decimal Lordo,
    decimal Trasferte,
    decimal Trattenute,
    decimal ContributiDipendente,
    decimal Arrotondamento,
    decimal Netto,
    IReadOnlyList<SegnalazioneBustaAgricola> Segnalazioni);

/// <summary>
/// Riepilogo mensile aziendale generato insieme alle buste: totali di ore, giornate, lordo,
/// trasferte, trattenute, arrotondamenti e netto, più l'elenco completo di WARNING/ERROR
/// del validatore. Tutti gli importi vengono dalle buste archiviate: qui si somma, non si
/// ricalcola.
/// </summary>
public sealed class RiepilogoAgricoloMensile
{
    private static readonly CultureInfo It = CultureInfo.GetCultureInfo("it-IT");

    public required int Anno { get; init; }
    public required int Mese { get; init; }
    public required string StatoElaborazione { get; init; }
    public string? ValidataDa { get; init; }
    public required IReadOnlyList<RigaRiepilogoAgricolo> Righe { get; init; }
    public required IReadOnlyList<SegnalazioneBustaAgricola> SegnalazioniMensili { get; init; }

    public decimal TotaleOre => Righe.Sum(r => r.Ore);
    public int TotaleGiornate => Righe.Sum(r => r.Giornate);
    public decimal TotaleLordo => Righe.Sum(r => r.Lordo);
    public decimal TotaleTrasferte => Righe.Sum(r => r.Trasferte);
    public decimal TotaleTrattenute => Righe.Sum(r => r.Trattenute);
    public decimal TotaleContributiDipendente => Righe.Sum(r => r.ContributiDipendente);
    public decimal TotaleArrotondamenti => Righe.Sum(r => r.Arrotondamento);
    public decimal TotaleNetto => Righe.Sum(r => r.Netto);

    public int Errori => TutteLeSegnalazioni.Count(s => s.Livello == LivelloSegnalazione.Errore);
    public int Avvisi => TutteLeSegnalazioni.Count(s => s.Livello == LivelloSegnalazione.Avviso);

    public IEnumerable<SegnalazioneBustaAgricola> TutteLeSegnalazioni =>
        Righe.SelectMany(r => r.Segnalazioni).Concat(SegnalazioniMensili);

    /// <summary>Versione CSV (separatore «;», decimali all'italiana) apribile in Excel.</summary>
    public string Csv()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Lavoratore;Ore;Giornate;Lordo;Trasferte;Trattenute;Contributi c/lav;Arrotondamento;Netto;Segnalazioni");
        foreach (var r in Righe)
            sb.AppendLine(string.Join(';',
                r.Nominativo,
                r.Ore.ToString("0.##", It),
                r.Giornate.ToString(It),
                r.Lordo.ToString("0.00", It),
                r.Trasferte.ToString("0.00", It),
                r.Trattenute.ToString("0.00", It),
                r.ContributiDipendente.ToString("0.00", It),
                r.Arrotondamento.ToString("0.00", It),
                r.Netto.ToString("0.00", It),
                string.Join(" | ", r.Segnalazioni.Select(s => $"{s.Livello} {s.Codice}"))));
        sb.AppendLine(string.Join(';',
            "TOTALE",
            TotaleOre.ToString("0.##", It),
            TotaleGiornate.ToString(It),
            TotaleLordo.ToString("0.00", It),
            TotaleTrasferte.ToString("0.00", It),
            TotaleTrattenute.ToString("0.00", It),
            TotaleContributiDipendente.ToString("0.00", It),
            TotaleArrotondamenti.ToString("0.00", It),
            TotaleNetto.ToString("0.00", It),
            $"{Errori} errori, {Avvisi} avvisi"));
        foreach (var s in SegnalazioniMensili)
            sb.AppendLine($"{s.Livello};{s.Codice};{s.Messaggio}");
        return sb.ToString();
    }
}
