using System.Globalization;
using System.Text;

namespace Paghe.Core.AdeTelematico;

/// <summary>
/// Bozza leggibile (NON un tracciato telematico ufficiale) dell'autoliquidazione INAIL: l'invio
/// avviene sui servizi online INAIL (Punto Cliente), non tramite Desktop Telematico. Il documento
/// serve da riepilogo da controllare o allegare manualmente.
/// </summary>
public sealed class BozzaLeggibileInailWriter
{
    public const string IntestazioneBozza = "### BOZZA NON UFFICIALE AUTOLIQUIDAZIONE INAIL - NON TRASMETTERE ###";

    public string Genera(int anno, string pat, string voceTariffa, RisultatoAutoliquidazioneInail r)
    {
        ArgumentNullException.ThrowIfNull(r);

        var sb = new StringBuilder();
        sb.AppendLine(IntestazioneBozza);
        sb.AppendLine($"Anno di riferimento: {anno}");
        sb.AppendLine($"PAT: {pat}");
        sb.AppendLine($"Voce di tariffa: {voceTariffa}");
        sb.AppendLine($"Tasso applicato: {r.TassoPerMille.ToString("0.000", CultureInfo.InvariantCulture)} per mille");
        sb.AppendLine($"Retribuzioni {anno}: {Importo(r.RetribuzioniAnno)}");
        sb.AppendLine($"Regolazione {anno}: {Importo(r.Regolazione)}");
        sb.AppendLine($"Rata anticipata {anno + 1}: {Importo(r.RataAnticipata)}");
        sb.AppendLine($"Rata già versata: -{Importo(r.RataGiaVersata)}");
        sb.AppendLine($"TOTALE DA VERSARE: {Importo(r.TotaleDaVersare)}");
        sb.AppendLine();
        sb.AppendLine("Nota: l'invio dell'autoliquidazione avviene sui servizi online INAIL (Punto Cliente).");
        sb.AppendLine("Questa bozza non è un tracciato telematico ufficiale.");
        return sb.ToString();
    }

    private static string Importo(decimal v) => v.ToString("0.00", CultureInfo.InvariantCulture);
}
