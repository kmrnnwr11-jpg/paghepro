using System.Globalization;
using System.Text;
using Paghe.Core.Domain;

namespace Paghe.Core.Calcolo;

/// <summary>Rende un cedolino in formato testo leggibile (per console/anteprima).</summary>
public static class StampaCedolino
{
    private static readonly CultureInfo It = CultureInfo.GetCultureInfo("it-IT");

    public static string Testo(Cedolino c, string intestazione)
    {
        var sb = new StringBuilder();
        string Eur(decimal v) => v.ToString("N2", It).PadLeft(12);

        sb.AppendLine("┌────────────────────────────────────────────────────────────────────┐");
        sb.AppendLine($"│ {intestazione,-66} │");
        sb.AppendLine($"│ Periodo: {c.Mese:00}/{c.Anno}{new string(' ', 56)}│");
        sb.AppendLine("├──────────────┬──────────────────────────────┬────────┬───────────┤");
        sb.AppendLine("│ Codice       │ Descrizione                  │  Q.tà  │   Importo │");
        sb.AppendLine("├──────────────┼──────────────────────────────┼────────┼───────────┤");

        foreach (var r in c.Righe)
        {
            string q = r.Quantita.HasValue ? r.Quantita.Value.ToString("0.##", It) : "";
            string segno = r.Tipo == TipoVoce.Trattenuta ? "-" : (r.Tipo == TipoVoce.Figurativa ? "*" : " ");
            string imp = Math.Abs(r.Importo).ToString("N2", It);
            sb.AppendLine($"│ {Trunc(r.Codice, 12),-12} │ {Trunc(r.Descrizione, 28),-28} │ {q,6} │ {segno}{imp,8} │");
        }

        sb.AppendLine("├──────────────┴──────────────────────────────┴────────┴───────────┤");
        sb.AppendLine($"│ Totale competenze ................................ {Eur(c.TotaleCompetenze)} │");
        sb.AppendLine($"│ Totale trattenute ................................ {Eur(c.TotaleTrattenute)} │");
        sb.AppendLine("│                                                                    │");
        sb.AppendLine($"│ Imponibile previdenziale (INPS) .................. {Eur(c.ImponibileInps)} │");
        sb.AppendLine($"│   Contributi c/dipendente ........................ {Eur(c.ContributiDipendente)} │");
        sb.AppendLine($"│ Imponibile fiscale (IRPEF) ....................... {Eur(c.ImponibileIrpef)} │");
        sb.AppendLine($"│   IRPEF lorda .................................... {Eur(c.IrpefLorda)} │");
        sb.AppendLine($"│   Detrazioni lavoro dipendente ................... {Eur(c.DetrazioniLavoroDipendente)} │");
        sb.AppendLine($"│   IRPEF netta ................................... {Eur(c.IrpefNetta)} │");
        sb.AppendLine($"│   Addizionale regionale .......................... {Eur(c.AddizionaleRegionale)} │");
        sb.AppendLine($"│   Addizionale comunale ........................... {Eur(c.AddizionaleComunale)} │");
        sb.AppendLine("├────────────────────────────────────────────────────────────────────┤");
        sb.AppendLine($"│ NETTO IN BUSTA .................................. {Eur(c.NettoInBusta)} │");
        sb.AppendLine("├────────────────────────────────────────────────────────────────────┤");
        sb.AppendLine($"│ Quota TFR del mese (*) ........................... {Eur(c.QuotaTfr)} │");
        sb.AppendLine($"│ Contributi c/datore (*) .......................... {Eur(c.ContributiDatore)} │");
        sb.AppendLine($"│ Costo totale azienda (*) ......................... {Eur(c.CostoAzienda)} │");
        sb.AppendLine("└────────────────────────────────────────────────────────────────────┘");
        sb.AppendLine("(*) = voci figurative, non incidono sul netto in busta.");

        return sb.ToString();
    }

    private static string Trunc(string s, int max) => s.Length <= max ? s : s[..(max - 1)] + "…";
}
