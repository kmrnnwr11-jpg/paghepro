using Microsoft.EntityFrameworkCore;
using Paghe.Documenti;
using Paghe.Persistence.Entities;

namespace Paghe.Persistence;

/// <summary>
/// Preferenze d'uso per utente, isolate per tenant. Ogni chiave è in whitelist con il suo
/// validatore: una chiave sconosciuta o un valore fuori lista vengono rifiutati, così la
/// tabella non diventa un deposito arbitrario. Le preferenze cambiano solo l'aspetto o
/// le proposte iniziali: mai i numeri dei documenti.
/// </summary>
public sealed class PreferenzeUtenteService
{
    public const string StileBusta = "stile_busta";
    public const string FontDocumenti = "font_documenti";
    public const string DicituraStudio = "dicitura_studio";
    public const string ColoreAccento = "colore_accento";
    public const string RisposteBreviAurora = "risposte_brevi_aurora";

    /// <summary>
    /// «Autorizzazione completa»: col consenso pieno e la strada A3 attiva, Aurora applica
    /// da sola le modifiche reversibili appena preparate (stessa catena firmata del
    /// protocollo A3: quota, audit, annullo 24 ore) invece di fermarsi alla card.
    /// </summary>
    public const string AutoApplicaAurora = "auto_applica_aurora";
    public const string ScalaTesto = "scala_testo";
    public const string FontInterfaccia = "font_ui";
    public const string Densita = "densita";

    /// <summary>Tinte ammesse per l'accento dei documenti: palette verificata, niente colori liberi.</summary>
    public static readonly IReadOnlyList<string> PaletteAccento =
        ["#12395E", "#1F5B45", "#7C2D12", "#4C1D95", "#0F766E", "#334155"];

    private const int DicituraMassima = 90;

    private static readonly IReadOnlyDictionary<string, Func<string, string?>> Validatori =
        new Dictionary<string, Func<string, string?>>(StringComparer.Ordinal)
        {
            [StileBusta] = Scelta(CatalogoStiliCedolino.Chiavi.ToArray()),
            [FontDocumenti] = Scelta("lato", "plex", "carlito"),
            [DicituraStudio] = ValidaDicitura,
            [ColoreAccento] = valore => PaletteAccento.Contains(valore, StringComparer.OrdinalIgnoreCase)
                ? null
                : "Colore non in palette.",
            [RisposteBreviAurora] = Scelta("0", "1"),
            [AutoApplicaAurora] = Scelta("0", "1"),
            [ScalaTesto] = Scelta("normale", "grande", "molto-grande"),
            [FontInterfaccia] = Scelta("sistema", "lato"),
            [Densita] = Scelta("comoda", "compatta"),
        };

    private readonly PagheDbContext _db;
    private readonly TimeProvider _clock;

    public PreferenzeUtenteService(PagheDbContext db, TimeProvider? clock = null)
    {
        _db = db;
        _clock = clock ?? TimeProvider.System;
    }

    public async Task<Dictionary<string, string>> LeggiTutteAsync(
        Guid tenantId, Guid utenteId, CancellationToken ct = default) =>
        await _db.PreferenzeUtenti.AsNoTracking()
            .Where(p => p.TenantId == tenantId && p.UtenteId == utenteId)
            .ToDictionaryAsync(p => p.Chiave, p => p.Valore, ct);

    /// <summary>Legge una preferenza (o il default) senza sollevare per l'assenza.</summary>
    public async Task<string> LeggiAsync(
        Guid tenantId, Guid utenteId, string chiave, string predefinito, CancellationToken ct = default)
    {
        var valore = await _db.PreferenzeUtenti.AsNoTracking()
            .Where(p => p.TenantId == tenantId && p.UtenteId == utenteId && p.Chiave == chiave)
            .Select(p => p.Valore)
            .FirstOrDefaultAsync(ct);
        return string.IsNullOrEmpty(valore) ? predefinito : valore;
    }

    /// <summary>Salva (upsert) una preferenza; valore vuoto = rimozione. Whitelist obbligatoria.</summary>
    public async Task SalvaAsync(
        Guid tenantId, Guid utenteId, string chiave, string valore, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty || utenteId == Guid.Empty)
            throw new ArgumentException("Utente non valido per le preferenze.");
        if (!Validatori.TryGetValue(chiave, out var valida))
            throw new ArgumentException($"Preferenza sconosciuta: {chiave}.");

        valore = (valore ?? "").Trim();
        var esistente = await _db.PreferenzeUtenti.FirstOrDefaultAsync(
            p => p.TenantId == tenantId && p.UtenteId == utenteId && p.Chiave == chiave, ct);

        if (valore.Length == 0)
        {
            if (esistente is not null)
            {
                _db.PreferenzeUtenti.Remove(esistente);
                await _db.SaveChangesAsync(ct);
            }
            return;
        }

        if (valida(valore) is { } errore)
            throw new ArgumentException($"Valore non valido per {chiave}: {errore}");

        if (esistente is null)
        {
            _db.PreferenzeUtenti.Add(new PreferenzaUtenteEntity
            {
                Id = Guid.NewGuid(), TenantId = tenantId, UtenteId = utenteId,
                Chiave = chiave, Valore = valore,
                AggiornatoIl = _clock.GetUtcNow().UtcDateTime,
            });
        }
        else
        {
            esistente.Valore = valore;
            esistente.AggiornatoIl = _clock.GetUtcNow().UtcDateTime;
        }
        await _db.SaveChangesAsync(ct);
    }

    private static Func<string, string?> Scelta(params string[] ammessi) =>
        valore => ammessi.Contains(valore, StringComparer.Ordinal)
            ? null
            : $"ammessi: {string.Join(", ", ammessi)}.";

    private static string? ValidaDicitura(string valore)
    {
        if (valore.Length > DicituraMassima)
            return $"massimo {DicituraMassima} caratteri.";
        if (valore.Any(char.IsControl))
            return "caratteri di controllo non ammessi.";
        return null;
    }
}
