using Paghe.Core.AdeTelematico;

namespace Paghe.Api;

/// <summary>
/// Lavoratore riconosciuto nei messaggi della chat dal parser UNILAV deterministico: la SPA
/// mostra la card «Registra questo dipendente» per il cliente selezionato. Nessun dato viene
/// salvato da qui; la registrazione è un click dell'utente su <c>/api/unilav/testo</c>.
/// </summary>
public sealed record LavoratoreRiconosciutoAi(
    string CodiceFiscale,
    string? Nome,
    string? Cognome,
    bool NomeCognomeDaCampoUnico,
    string? DataAssunzione,
    string? Ccnl,
    string? Livello,
    string? Qualifica,
    string? TipoContratto,
    decimal? OreSettimanali)
{
    /// <summary>
    /// Riconoscimento SOLO deterministico da un singolo messaggio. Serve il codice fiscale
    /// personale a 16 caratteri più il nome o il cognome del lavoratore: un CF isolato — o
    /// il CF del titolare in una visura, dove «data inizio» è quella dell'attività — non è
    /// una richiesta di assunzione.
    /// </summary>
    internal static LavoratoreRiconosciutoAi? RiconosciNelTesto(string? testo)
    {
        if (string.IsNullOrWhiteSpace(testo)) return null;
        var esito = ImportazioneUnilavPdf.Importa(testo);
        var lavoratore = esito.Lavoratore;
        string cf = (lavoratore.CodiceFiscale ?? "").Trim().ToUpperInvariant();
        if (cf.Length != 16 || !ValidatoreFiscale.CodiceFiscaleValido(cf)) return null;
        bool altriDati = !string.IsNullOrWhiteSpace(lavoratore.Nome)
            || !string.IsNullOrWhiteSpace(lavoratore.Cognome);
        if (!altriDati) return null;
        return new(
            cf,
            lavoratore.Nome,
            lavoratore.Cognome,
            lavoratore.NomeCognomeDaCampoUnico,
            esito.Rapporto.DataInizio?.ToString("yyyy-MM-dd"),
            esito.Rapporto.Ccnl,
            esito.Rapporto.Livello,
            esito.Rapporto.Qualifica,
            esito.Rapporto.TipoContratto,
            esito.Rapporto.OreSettimanali);
    }
}
