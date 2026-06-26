using System.Globalization;
using System.Text;

namespace Paghe.Core.AdeTelematico;

/// <summary>Genera il file telematico di un modello F24.</summary>
public interface ITracciatoF24Writer
{
    string Genera(ModelloF24 f24);
}

/// <summary>
/// BOZZA LEGGIBILE non ufficiale del modello F24, per sviluppo e test del flusso interno.
/// <para>
/// NON è il tracciato telematico ufficiale e NON va trasmesso. Il formato ufficiale è a
/// lunghezza fissa con record A/M/V/Z e va implementato dalle specifiche tecniche ufficiali
/// dell'Agenzia delle Entrate (cartella <c>agenzia-entrate</c> / <c>specifiche</c> del kit,
/// non incluse nel repository). Vedi docs/integrazione-ufficiale/.
/// </para>
/// </summary>
public sealed class BozzaLeggibileF24Writer : ITracciatoF24Writer
{
    public const string IntestazioneBozza = "### BOZZA NON UFFICIALE F24 - NON TRASMETTERE ###";

    public string Genera(ModelloF24 f24)
    {
        ArgumentNullException.ThrowIfNull(f24);

        var sb = new StringBuilder();
        sb.AppendLine(IntestazioneBozza);
        sb.AppendLine($"Contribuente: {f24.Contribuente.CodiceFiscale} ({f24.Contribuente.Tipo})");
        if (f24.Intermediario is { } i)
            sb.AppendLine($"Intermediario: {i.CodiceFiscale}");
        sb.AppendLine($"Data versamento: {f24.DataVersamento:yyyy-MM-dd}");

        foreach (var v in f24.Erario)
            Riga(sb, "ERARIO", v.CodiceTributo, v.AnnoRiferimento.ToString(), v.ImportoDebito, v.ImportoCredito);
        foreach (var v in f24.Inps)
            Riga(sb, "INPS", $"{v.CausaleContributo}/{v.MatricolaCodice}", v.PeriodoDa, v.ImportoDebito, v.ImportoCredito);
        foreach (var v in f24.Regioni)
            Riga(sb, "REGIONI", v.CodiceTributo, v.AnnoRiferimento.ToString(), v.ImportoDebito, v.ImportoCredito);
        foreach (var v in f24.TributiLocali)
            Riga(sb, "LOCALI", v.CodiceTributo, v.AnnoRiferimento.ToString(), v.ImportoDebito, v.ImportoCredito);
        foreach (var v in f24.AltriEnti)
            Riga(sb, "ALTRI", v.CausaleContributo, v.CodicePosizione, v.ImportoDebito, v.ImportoCredito);

        sb.AppendLine($"TOTALE DEBITI: {Importo(f24.TotaleDebiti)}");
        sb.AppendLine($"TOTALE CREDITI: {Importo(f24.TotaleCrediti)}");
        sb.AppendLine($"SALDO: {Importo(f24.SaldoFinale)}");
        return sb.ToString();
    }

    private static void Riga(StringBuilder sb, string sezione, string codice, string periodo, decimal debito, decimal credito) =>
        sb.AppendLine($"{sezione}\t{codice}\t{periodo}\tD={Importo(debito)}\tC={Importo(credito)}");

    private static string Importo(decimal v) => v.ToString("0.00", CultureInfo.InvariantCulture);
}
