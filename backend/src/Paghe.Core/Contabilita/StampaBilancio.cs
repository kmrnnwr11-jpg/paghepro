using System.Globalization;
using System.Text;

namespace Paghe.Core.Contabilita;

/// <summary>Rende un bilancio riclassificato in formato testo leggibile.</summary>
public static class StampaBilancio
{
    private static readonly CultureInfo It = CultureInfo.GetCultureInfo("it-IT");

    public static string Testo(Bilancio b, string intestazione)
    {
        string E(decimal v) => v.ToString("N2", It).PadLeft(14);
        var sb = new StringBuilder();

        sb.AppendLine($"=== {intestazione} ===");
        sb.AppendLine();
        sb.AppendLine("CONTO ECONOMICO (art. 2425 c.c.)");
        sb.AppendLine($"  A) Valore della produzione ............ {E(b.ValoreDellaProduzione)}");
        sb.AppendLine($"  B) Costi della produzione ............. {E(b.CostiDellaProduzione)}");
        sb.AppendLine($"  A-B) Differenza ....................... {E(b.DifferenzaValoreCosti)}");
        sb.AppendLine($"  C) Proventi e oneri finanziari ........ {E(b.ProventiOneriFinanziari)}");
        sb.AppendLine($"  Risultato prima delle imposte ......... {E(b.RisultatoPrimaDelleImposte)}");
        sb.AppendLine($"  20) Imposte ........................... {E(b.Imposte)}");
        sb.AppendLine($"  21) UTILE/(PERDITA) D'ESERCIZIO ....... {E(b.UtilePerditaEsercizio)}");
        sb.AppendLine();
        sb.AppendLine("STATO PATRIMONIALE — ATTIVO (art. 2424 c.c.)");
        sb.AppendLine($"  A) Crediti verso soci ................. {E(b.CreditiVersoSoci)}");
        sb.AppendLine($"  B) Immobilizzazioni ................... {E(b.Immobilizzazioni)}");
        sb.AppendLine($"  C) Attivo circolante .................. {E(b.AttivoCircolante)}");
        sb.AppendLine($"  D) Ratei e risconti attivi ........... {E(b.RateiRiscontiAttivi)}");
        sb.AppendLine($"  TOTALE ATTIVO ......................... {E(b.TotaleAttivo)}");
        sb.AppendLine();
        sb.AppendLine("STATO PATRIMONIALE — PASSIVO");
        sb.AppendLine($"  A) Patrimonio netto (incl. utile) ..... {E(b.PatrimonioNetto)}");
        sb.AppendLine($"  B) Fondi per rischi e oneri ........... {E(b.FondiRischiOneri)}");
        sb.AppendLine($"  C) Trattamento di fine rapporto ....... {E(b.TrattamentoFineRapporto)}");
        sb.AppendLine($"  D) Debiti ............................. {E(b.Debiti)}");
        sb.AppendLine($"  E) Ratei e risconti passivi ........... {E(b.RateiRiscontiPassivi)}");
        sb.AppendLine($"  TOTALE PASSIVO ........................ {E(b.TotalePassivo)}");
        sb.AppendLine();
        sb.AppendLine($"  Quadratura (Attivo - Passivo) ......... {E(b.SbilancioAttivoPassivo)}  -> {(b.Quadra ? "OK" : "NON QUADRA")}");

        return sb.ToString();
    }
}
