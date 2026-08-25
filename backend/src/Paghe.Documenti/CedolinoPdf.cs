using Paghe.Core.Domain;

namespace Paghe.Documenti;

/// <summary>
/// Facciata stabile per il cedolino PDF. La composizione professionale è separata
/// dall'adattamento dei dati, così prospetto e allegato mostrano gli stessi valori.
/// </summary>
public static class CedolinoPdf
{
    /// <summary>Variante minima per l'endpoint stateless e i test.</summary>
    public static byte[] Genera(Cedolino cedolino, string azienda, string dipendente) =>
        Genera(cedolino, new IntestazioneCedolino { Azienda = azienda, Dipendente = dipendente }, null);

    public static byte[] Genera(Cedolino cedolino, IntestazioneCedolino intestazione) =>
        Genera(cedolino, intestazione, null);

    public static byte[] Genera(
        Cedolino cedolino,
        IntestazioneCedolino intestazione,
        Cartellino? cartellino,
        ContestoEmissioneCedolino? emissione = null,
        StileCedolino stile = StileCedolino.Classico,
        ProgressiviAnnui? progressivi = null)
    {
        ArgumentNullException.ThrowIfNull(cedolino);
        ArgumentNullException.ThrowIfNull(intestazione);

        DocumentiConfig.Inizializza();
        var presentazione = CedolinoPdfPresentazione.Crea(
            cedolino, intestazione, cartellino, emissione ?? ContestoEmissioneCedolino.Bozza,
            progressivi);
        return stile switch
        {
            StileCedolino.Colori => CedolinoColoriPdf.Genera(presentazione),
            StileCedolino.Editoriale => CedolinoEditorialePdf.Genera(presentazione),
            _ => CedolinoProfessionalePdf.Genera(presentazione),
        };
    }
}

/// <summary>
/// Aspetto della busta paga. Il contenuto è identico nei tre stili: cambia solo
/// l'impaginazione, così lo studio sceglie senza che i numeri cambino.
/// </summary>
public enum StileCedolino
{
    /// <summary>Griglia del modulo cartaceo tradizionale, monocromatica.</summary>
    Classico,

    /// <summary>Impaginazione a sezioni numerate con testate blu e riquadro di sintesi.</summary>
    Colori,

    /// <summary>Impaginazione editoriale estesa con prospetto, dettagli e guida di lettura.</summary>
    Editoriale,
}

/// <summary>
/// Metadati di emissione separati dai calcoli. L'immagine è sempre caricata dal datore e non
/// viene mai generata dal software; non equivale a una firma digitale qualificata.
/// </summary>
public sealed record ContestoEmissioneCedolino(
    string Stato,
    byte[]? FirmaSiglaTimbro = null,
    string? RiferimentoDocumento = null,
    string? ValidazioneRegistrata = null)
{
    public static ContestoEmissioneCedolino Bozza { get; } = new("Bozza");
}
