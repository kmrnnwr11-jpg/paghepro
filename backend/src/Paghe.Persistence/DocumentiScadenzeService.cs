using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;

namespace Paghe.Persistence;

/// <summary>Risultato del parsing di un documento scadenza.</summary>
public sealed record EsitoParsingScadenza(
    bool AziendaTrovata,
    string? RagioneSociale,
    string? PartitaIva,
    DateOnly? DataScadenza,
    string? NumeroDocumento,
    Guid? PromemoriaCreatoId,
    string? Errore);

/// <summary>
/// Analizza il testo estratto da un documento (DURC, F24, ecc.), identifica l'azienda
/// per P.IVA e crea automaticamente il DURC + promemoria di rinnovo.
/// Il testo deve essere già estratto dal PDF (usa EstrattoreTestoPdf nell'endpoint).
/// </summary>
public sealed class DocumentiScadenzeService
{
    private readonly PagheDbContext _db;
    private readonly ScadenzarioService _scadenzario;

    public DocumentiScadenzeService(PagheDbContext db, ScadenzarioService scadenzario)
    {
        _db = db;
        _scadenzario = scadenzario;
    }

    public async Task<EsitoParsingScadenza> ParseDocumentoAsync(
        string testo,
        Guid tenantId,
        Guid? aziendaIdForzata = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(testo))
            return new EsitoParsingScadenza(false, null, null, null, null, null,
                "Il documento non contiene testo leggibile (potrebbe essere una scansione senza OCR).");

        // --- Estrai P.IVA ---
        string? piva = null;
        var pivaMatch = Regex.Match(testo, @"\b(\d{11})\b");
        if (pivaMatch.Success) piva = pivaMatch.Value;

        // --- Estrai numero documento ---
        string? numero = null;
        var numMatch = Regex.Match(testo, @"(?:Numero|Prot\.?|N\.?)\s*[:/]?\s*([A-Z0-9\-/]{5,30})", RegexOptions.IgnoreCase);
        if (numMatch.Success) numero = numMatch.Groups[1].Value.Trim();

        // --- Estrai date ---
        DateOnly? dataEmissione = null;
        DateOnly? dataScadenza = null;
        var dateMatches = Regex.Matches(testo, @"(\d{2})[/.\-](\d{2})[/.\-](\d{4})");
        var date = dateMatches.Cast<Match>()
            .Select(m => TryParseDate(m.Value))
            .Where(d => d.HasValue)
            .Select(d => d!.Value)
            .OrderBy(d => d)
            .ToList();

        // Strategia: cerca label esplicite per emissione e scadenza
        var emissMatch = Regex.Match(testo, @"(?:emiss[io]+ne|rilascio|data\s+d[i']?\s*emiss[io]+ne)[^\d]*(\d{2}[/.\-]\d{2}[/.\-]\d{4})", RegexOptions.IgnoreCase);
        if (emissMatch.Success) dataEmissione = TryParseDate(emissMatch.Groups[1].Value);

        var scadMatch = Regex.Match(testo, @"(?:scad[ae]|valid[ao]\s+(?:fino\s+al|al|sino\s+al)|scadenza)[^\d]*(\d{2}[/.\-]\d{2}[/.\-]\d{4})", RegexOptions.IgnoreCase);
        if (scadMatch.Success) dataScadenza = TryParseDate(scadMatch.Groups[1].Value);

        // Fallback: se date.Count >= 2, la più piccola = emissione, la più grande = scadenza
        if (dataEmissione is null && date.Count >= 1) dataEmissione = date[0];
        if (dataScadenza is null && date.Count >= 2) dataScadenza = date[^1];

        // Scadenza calcolata come emissione + 120 gg se non trovata esplicitamente
        if (dataScadenza is null && dataEmissione.HasValue)
            dataScadenza = dataEmissione.Value.AddDays(ScadenzarioService.GiorniValiditaDurc);

        // --- Trova azienda ---
        Entities.AziendaEntity? azienda = null;
        if (aziendaIdForzata.HasValue)
        {
            azienda = await _db.Aziende
                .FirstOrDefaultAsync(a => a.Id == aziendaIdForzata.Value && a.TenantId == tenantId, ct);
        }
        else if (piva is not null)
        {
            azienda = await _db.Aziende
                .FirstOrDefaultAsync(a => a.TenantId == tenantId && a.PartitaIva == piva, ct);
        }

        if (azienda is not null && aziendaIdForzata.HasValue && piva is not null
            && !string.IsNullOrWhiteSpace(azienda.PartitaIva)
            && !string.Equals(azienda.PartitaIva, piva, StringComparison.OrdinalIgnoreCase))
        {
            return new EsitoParsingScadenza(false, azienda.RagioneSociale, piva, dataScadenza, numero, null,
                $"La P.IVA del documento ({piva}) non corrisponde all'azienda selezionata ({azienda.PartitaIva}).");
        }

        if (azienda is null)
            return new EsitoParsingScadenza(false, null, piva, dataScadenza, numero, null,
                piva is not null
                    ? $"Nessuna azienda trovata con P.IVA {piva}. Selezionare l'azienda manualmente."
                    : "P.IVA non trovata nel documento. Selezionare l'azienda manualmente.");

        // --- Crea DURC e promemoria ---
        if (dataEmissione is null)
            return new EsitoParsingScadenza(true, azienda.RagioneSociale, piva, null, numero, null,
                "Data di emissione non trovata nel documento.");

        var numeroDoc = numero ?? $"DOC-{DateTime.UtcNow:yyyyMMddHHmmss}";
        try
        {
            var durc = await _scadenzario.RegistraDurcAsync(tenantId, new DurcInput(
                azienda.Id, numeroDoc, dataEmissione.Value, dataScadenza, true), ct);

            await _scadenzario.RigeneraAsync(tenantId, ct);

            var promemoria = await _db.Promemoria
                .FirstOrDefaultAsync(p => p.TenantId == tenantId
                    && p.RiferimentoId == durc.Id.ToString(), ct);

            return new EsitoParsingScadenza(
                true, azienda.RagioneSociale, piva,
                durc.DataScadenza, durc.Numero, promemoria?.Id, null);
        }
        catch (ArgumentException ex)
        {
            return new EsitoParsingScadenza(true, azienda.RagioneSociale, piva, dataScadenza, numero, null, ex.Message);
        }
    }

    private static DateOnly? TryParseDate(string s)
    {
        s = s.Replace('.', '/').Replace('-', '/');
        if (DateOnly.TryParseExact(s, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
            return d;
        return null;
    }
}
