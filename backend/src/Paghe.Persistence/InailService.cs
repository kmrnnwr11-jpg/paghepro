using Microsoft.EntityFrameworkCore;
using Paghe.Core.AdeTelematico;
using Paghe.Core.Domain;
using Paghe.Persistence.Cedolini;
using Paghe.Persistence.Entities;

namespace Paghe.Persistence;

/// <summary>Bozza di autoliquidazione INAIL per un'azienda e un anno.</summary>
public sealed record AutoliquidazioneInailBozza(
    int Anno,
    string Pat,
    string VoceTariffa,
    int NumeroCedolini,
    decimal RetribuzioniAnno,
    decimal TassoPerMille,
    decimal Regolazione,
    decimal RataAnticipata,
    decimal RataGiaVersata,
    decimal TotaleDaVersare,
    string Nota);

/// <summary>
/// Posizione assicurativa INAIL (PAT + tasso dalla lettera "basi di calcolo") e calcolo
/// dell'autoliquidazione: le retribuzioni dell'anno arrivano dai cedolini archiviati
/// (semplificazione: base = imponibile INPS). L'invio resta sui servizi INAIL online.
/// Isolato per tenant.
/// </summary>
public sealed class InailService
{
    private const string NotaBozza =
        "Bozza di calcolo: l'invio dell'autoliquidazione resta sui servizi INAIL online; "
        + "tasso e basi dalla lettera 'basi di calcolo' INAIL.";

    private readonly PagheDbContext _db;
    public InailService(PagheDbContext db) => _db = db;

    public Task<PosizioneInailEntity?> GetPosizioneAsync(
        Guid tenantId, Guid aziendaId, int anno, CancellationToken ct = default) =>
        _db.PosizioniInail.FirstOrDefaultAsync(
            p => p.TenantId == tenantId && p.AziendaId == aziendaId && p.Anno == anno, ct);

    /// <summary>Crea o aggiorna la posizione INAIL dell'azienda per l'anno (upsert).</summary>
    public async Task<PosizioneInailEntity> SalvaPosizioneAsync(
        Guid tenantId, Guid aziendaId, int anno, string pat, string voceTariffa,
        decimal tassoPerMille, CancellationToken ct = default)
    {
        bool aziendaOk = await _db.Aziende.AnyAsync(
            a => a.Id == aziendaId && a.TenantId == tenantId, ct);
        if (!aziendaOk)
            throw new ArgumentException("Azienda non trovata per il tenant indicato.");
        if (tassoPerMille <= 0m)
            throw new ArgumentException("Il tasso INAIL (per mille) deve essere maggiore di zero.");

        var posizione = await GetPosizioneAsync(tenantId, aziendaId, anno, ct);
        if (posizione is null)
        {
            posizione = new PosizioneInailEntity
            {
                Id = Guid.NewGuid(), TenantId = tenantId, AziendaId = aziendaId, Anno = anno,
            };
            _db.PosizioniInail.Add(posizione);
        }

        posizione.Pat = pat.Trim();
        posizione.VoceTariffa = voceTariffa.Trim();
        posizione.TassoPerMille = tassoPerMille;
        posizione.AggiornataIl = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);
        return posizione;
    }

    /// <summary>
    /// Calcola la bozza di autoliquidazione dell'anno: somma le retribuzioni imponibili dei
    /// cedolini archiviati dell'azienda e applica il tasso della posizione salvata.
    /// </summary>
    public async Task<AutoliquidazioneInailBozza> CalcolaAutoliquidazioneAsync(
        Guid tenantId, Guid aziendaId, int anno, decimal rataGiaVersata = 0m,
        CancellationToken ct = default)
    {
        var posizione = await GetPosizioneAsync(tenantId, aziendaId, anno, ct)
            ?? throw new ArgumentException(
                $"Posizione INAIL non registrata per l'anno {anno}: salvare prima PAT e tasso.");

        var cedolini = await _db.Cedolini.UtilizzabiliAValle()
            .Where(c => c.TenantId == tenantId && c.AziendaId == aziendaId
                && c.Anno == anno)
            .ToListAsync(ct);
        if (cedolini.Count == 0)
            throw new ArgumentException(
                $"Nessun cedolino chiuso o storico pre-workflow per l'anno {anno}.");

        decimal retribuzioni = cedolini.Sum(c => c.ImponibileInps);
        var r = AutoliquidazioneInail.Calcola(retribuzioni, posizione.TassoPerMille, rataGiaVersata);

        return new AutoliquidazioneInailBozza(
            anno, posizione.Pat, posizione.VoceTariffa, cedolini.Count,
            r.RetribuzioniAnno, r.TassoPerMille, r.Regolazione, r.RataAnticipata,
            r.RataGiaVersata, r.TotaleDaVersare, NotaBozza);
    }
}
