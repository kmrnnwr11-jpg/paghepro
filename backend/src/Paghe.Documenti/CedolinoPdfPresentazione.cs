using System.Globalization;
using Paghe.Core.Domain;

namespace Paghe.Documenti;

/// <summary>
/// Dati già classificati per la stampa. Non contiene calcoli normativi: rende soltanto
/// esplicita la semantica delle righe e protegge i dati che non vanno stampati per esteso.
/// </summary>
internal sealed record CedolinoPresentazione(
    Cedolino Cedolino,
    IntestazioneCedolino Intestazione,
    Cartellino? Cartellino,
    ContestoEmissioneCedolino Emissione,
    string Periodo,
    IReadOnlyList<RigaCedolinoPresentata> Righe,
    string? IbanMascherato);

internal sealed record RigaCedolinoPresentata(
    string Codice,
    string Descrizione,
    decimal? Quantita,
    decimal? Base,
    decimal? Competenza,
    decimal? Trattenuta);

internal static class CedolinoPdfPresentazione
{
    private static readonly CultureInfo It = CultureInfo.GetCultureInfo("it-IT");

    internal static CedolinoPresentazione Crea(
        Cedolino cedolino,
        IntestazioneCedolino intestazione,
        Cartellino? cartellino,
        ContestoEmissioneCedolino? emissione = null)
    {
        emissione ??= ContestoEmissioneCedolino.Bozza;
        string periodo = cedolino.Mese is >= 1 and <= 12
            ? new DateTime(cedolino.Anno, cedolino.Mese, 1).ToString("MMMM yyyy", It)
            : $"{cedolino.Mese:00}/{cedolino.Anno}";

        var righe = cedolino.Righe
            // Anche le righe a importo zero vengono conservate: il renderer le sposta
            // nei dettagli informativi senza simularle come competenze economiche.
            .Where(riga => riga.Tipo != TipoVoce.Figurativa)
            .Select(riga => riga.Tipo == TipoVoce.Trattenuta
                ? new RigaCedolinoPresentata(
                    riga.Codice, riga.Descrizione, riga.Quantita, riga.Base,
                    Competenza: null, Trattenuta: Math.Abs(riga.Importo))
                : new RigaCedolinoPresentata(
                    riga.Codice, riga.Descrizione, riga.Quantita, riga.Base,
                    Competenza: riga.Importo, Trattenuta: null))
            .ToList();

        return new CedolinoPresentazione(
            cedolino,
            intestazione,
            cartellino,
            emissione,
            periodo,
            righe,
            MascheraIban(intestazione.IbanDipendente));
    }

    internal static string? MascheraIban(string? iban)
    {
        if (string.IsNullOrWhiteSpace(iban)) return null;

        string normalizzato = new(iban
            .Where(char.IsLetterOrDigit)
            .Select(char.ToUpperInvariant)
            .ToArray());

        if (normalizzato.Length <= 4) return "****";
        if (normalizzato.Length <= 8) return $"**** {normalizzato[^4..]}";

        return $"{normalizzato[..4]} **** **** **** **** {normalizzato[^4..]}";
    }
}
