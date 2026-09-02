using Microsoft.EntityFrameworkCore;
using Paghe.Persistence.Cedolini;
using Paghe.Persistence.Entities;
using Paghe.Persistence.Gdpr;

namespace Paghe.Persistence;

/// <summary>Busta agricola archiviata, pronta per essere riletta o riscaricata.</summary>
public sealed record DocumentoCedolinoAgricolo(byte[] Pdf, string NomeFile, string HashSha256);

/// <summary>
/// Archivio delle buste paga agricole, isolato per tenant e <b>separato</b> da quello dei
/// cedolini ordinari (vedi <see cref="CedolinoAgricoloEntity"/>: i flussi F24/UniEmens/CU
/// leggono la tabella <c>Cedolini</c> e una busta agricola lì dentro produrrebbe adempimenti
/// sbagliati). Conserva il PDF consegnato e i dati che l'hanno generato.
/// </summary>
public sealed class CedoliniAgricoliService
{
    private readonly PagheDbContext _db;
    private readonly TimeProvider _clock;

    public CedoliniAgricoliService(PagheDbContext db, TimeProvider? clock = null)
    {
        _db = db;
        _clock = clock ?? TimeProvider.System;
    }

    /// <summary>
    /// Archivia (o riarchivia dopo una correzione) la busta agricola del periodo. La coppia
    /// rapporto+periodo è unica: una nuova archiviazione sostituisce la precedente e alza la
    /// versione, così l'archivio non racconta due mesi diversi per lo stesso lavoratore.
    /// </summary>
    public async Task<CedolinoAgricoloEntity> ArchiviaAsync(
        Guid tenantId,
        Guid attoreId,
        CedolinoAgricoloEntity busta,
        byte[] pdf,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(busta);
        if (tenantId == Guid.Empty) throw new ArgumentException("Tenant non valido.", nameof(tenantId));
        if (attoreId == Guid.Empty) throw new ArgumentException("Utente non valido.", nameof(attoreId));
        if (pdf is not { Length: > 0 }) throw new ArgumentException("PDF della busta mancante.", nameof(pdf));

        // Il rapporto deve essere del tenant e dell'azienda dichiarata: l'archivio non accetta
        // buste appese a un rapporto altrui.
        var rapporto = await _db.Rapporti.AsNoTracking().FirstOrDefaultAsync(
            r => r.Id == busta.RapportoLavoroId && r.TenantId == tenantId, ct)
            ?? throw new ArgumentException("Rapporto agricolo non trovato per il tenant indicato.");
        if (rapporto.AziendaId != busta.AziendaId)
            throw new ArgumentException("Il rapporto non appartiene all'azienda indicata.");

        var esistente = await _db.CedoliniAgricoli.FirstOrDefaultAsync(
            c => c.TenantId == tenantId && c.RapportoLavoroId == busta.RapportoLavoroId
                && c.Anno == busta.Anno && c.Mese == busta.Mese, ct);

        var id = esistente?.Id ?? Guid.NewGuid();
        busta.Id = id;
        busta.TenantId = tenantId;
        busta.ArchiviatoIl = _clock.GetUtcNow().UtcDateTime;
        busta.ArchiviatoDa = attoreId;
        // La validazione è dichiarata contestualmente all'archiviazione: senza nominativo
        // non esiste, e la data è quella dell'atto.
        busta.ValidataDa = string.IsNullOrWhiteSpace(busta.ValidataDa)
            ? null
            : busta.ValidataDa.Trim();
        busta.ValidataIl = busta.ValidataDa is null ? null : busta.ArchiviatoIl;
        busta.Versione = (esistente?.Versione ?? 0) + 1;
        busta.HashPdfSha256 = CedolinoSnapshotCodec.Hash(pdf);
        busta.PdfProtetto = _db.Protettore.ProteggiConContesto(
            Convert.ToBase64String(pdf),
            ContestoDatiProtetti.PdfCedolinoAgricolo(tenantId, id))!;

        if (esistente is null)
        {
            _db.CedoliniAgricoli.Add(busta);
        }
        else
        {
            _db.Entry(esistente).CurrentValues.SetValues(busta);
        }
        await _db.SaveChangesAsync(ct);
        return esistente ?? busta;
    }

    /// <summary>Buste archiviate di un'azienda, dalla più recente; filtrabili per periodo.</summary>
    public async Task<List<CedolinoAgricoloEntity>> ElencoAsync(
        Guid tenantId, Guid aziendaId, int? anno = null, int? mese = null,
        CancellationToken ct = default)
    {
        var query = _db.CedoliniAgricoli.AsNoTracking()
            .Where(c => c.TenantId == tenantId && c.AziendaId == aziendaId);
        if (anno is { } a) query = query.Where(c => c.Anno == a);
        if (mese is { } m) query = query.Where(c => c.Mese == m);
        return await query
            .OrderByDescending(c => c.Anno).ThenByDescending(c => c.Mese)
            .ThenByDescending(c => c.ArchiviatoIl)
            .ToListAsync(ct);
    }

    /// <summary>
    /// Rilegge il PDF archiviato verificandone l'impronta: se il documento non è quello di
    /// allora, meglio un errore che una busta silenziosamente diversa.
    /// </summary>
    public async Task<DocumentoCedolinoAgricolo?> LeggiPdfAsync(
        Guid tenantId, Guid id, CancellationToken ct = default)
    {
        var busta = await _db.CedoliniAgricoli.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id && c.TenantId == tenantId, ct);
        if (busta is null) return null;

        string base64 = _db.Protettore.SvelaConContesto(
            busta.PdfProtetto, ContestoDatiProtetti.PdfCedolinoAgricolo(tenantId, busta.Id))
            ?? throw new InvalidOperationException("PDF della busta agricola non leggibile.");
        byte[] pdf = Convert.FromBase64String(base64);
        if (!string.Equals(CedolinoSnapshotCodec.Hash(pdf), busta.HashPdfSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                "L'impronta del PDF archiviato non corrisponde: documento non integro.");

        var nominativo = await _db.Rapporti.AsNoTracking()
            .Where(r => r.Id == busta.RapportoLavoroId && r.TenantId == tenantId
                && r.Dipendente != null)
            .Select(r => r.Dipendente!.Cognome + " " + r.Dipendente.Nome)
            .FirstOrDefaultAsync(ct);

        return new DocumentoCedolinoAgricolo(
            pdf,
            NomeFileBusta(nominativo, busta.Anno, busta.Mese, "archiviata"),
            busta.HashPdfSha256);
    }

    /// <summary>
    /// Nome file leggibile del PDF: lavoratore e periodo, senza caratteri vietati dai
    /// filesystem (es. <c>busta-agricola-Rossi-Mario-2026-07-archiviata.pdf</c>).
    /// </summary>
    public static string NomeFileBusta(string? nominativo, int anno, int mese, string suffisso)
    {
        string pulito = string.Concat((nominativo ?? "").Trim().Split(Path.GetInvalidFileNameChars()))
            .Replace(' ', '-');
        string chi = pulito.Length == 0 ? "" : $"-{pulito}";
        return $"busta-agricola{chi}-{anno}-{mese:00}-{suffisso}.pdf";
    }
}
