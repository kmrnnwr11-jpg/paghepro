using Paghe.Core.Calcolo;

namespace Paghe.Core.AdeTelematico;

/// <summary>
/// Trasforma un calcolo di ravvedimento in righe della sezione Erario dell'F24: tributo, sanzione e
/// interessi come tre <see cref="VoceErario"/>. I codici tributo di sanzione e interessi dipendono dal
/// tributo originario (es. per l'IRPEF a saldo o le ritenute) e sono <b>passati dal chiamante</b>:
/// non sono cablati qui, vanno presi dalla tabella ufficiale dei codici tributo dell'Agenzia Entrate.
/// </summary>
public static class RavvedimentoF24
{
    /// <summary>
    /// Costruisce le righe Erario per il ravvedimento. Le voci con importo zero (es. interessi nulli)
    /// vengono omesse. <paramref name="annoRiferimento"/> è l'anno d'imposta del tributo.
    /// </summary>
    public static IReadOnlyList<VoceErario> VociErario(
        RisultatoRavvedimento r,
        string codiceTributo,
        int annoRiferimento,
        string codiceSanzione,
        string codiceInteressi)
    {
        var voci = new List<VoceErario>
        {
            new(codiceTributo, annoRiferimento, ImportoDebito: r.Tributo),
        };
        if (r.Sanzione > 0)
            voci.Add(new VoceErario(codiceSanzione, annoRiferimento, ImportoDebito: r.Sanzione));
        if (r.Interessi > 0)
            voci.Add(new VoceErario(codiceInteressi, annoRiferimento, ImportoDebito: r.Interessi));
        return voci;
    }
}
