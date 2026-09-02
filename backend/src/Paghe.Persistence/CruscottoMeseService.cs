using Microsoft.EntityFrameworkCore;
using Paghe.Core.Domain;

namespace Paghe.Persistence;

public sealed record RigaCruscottoRapporto(
    Guid RapportoId, string Nominativo, string Stato, string Motivo);

public sealed record AziendaCruscotto(
    Guid AziendaId,
    string RagioneSociale,
    int Elaborati,
    int Pronti,
    int DaCompilare,
    int Bloccati,
    int Agricoli,
    IReadOnlyList<RigaCruscottoRapporto> Rapporti);

public sealed record CruscottoMese(
    int Anno,
    int Mese,
    int AziendeTotali,
    bool ElencoTagliato,
    IReadOnlyList<AziendaCruscotto> Aziende);

/// <summary>
/// Cruscotto del mese di tutto lo studio: per ogni azienda, lo stato operativo di ciascun
/// rapporto con la stessa tassonomia del broker Aurora (protetto / gia_elaborato / agricolo /
/// bloccato / da_compilare / cartellino_presente) così le due viste non si contraddicono mai.
/// Sola lettura, isolato per tenant; non elabora e non modifica nulla.
/// </summary>
public sealed class CruscottoMeseService
{
    // Le tenant molto grandi non servono a questa vista d'insieme: il taglio è dichiarato
    // nella risposta, come per l'archivio clienti di Aurora.
    private const int MassimoAziende = 100;

    private readonly PagheDbContext _db;

    public CruscottoMeseService(PagheDbContext db) => _db = db;

    public async Task<CruscottoMese> CostruisciAsync(
        Guid tenantId, int anno, int mese, CancellationToken ct = default)
    {
        if (anno is < 2000 or > 2200 || mese is < 1 or > 12)
            throw new ArgumentException("Periodo non valido: indica un anno e un mese reali.");
        var inizio = new DateOnly(anno, mese, 1);
        var fine = inizio.AddMonths(1).AddDays(-1);

        var aziendeTotali = await _db.Aziende.AsNoTracking()
            .CountAsync(a => a.TenantId == tenantId, ct);
        var aziende = await _db.Aziende.AsNoTracking()
            .Where(a => a.TenantId == tenantId)
            .OrderBy(a => a.RagioneSociale)
            .Take(MassimoAziende)
            .Select(a => new
            {
                a.Id,
                a.RagioneSociale,
                Completa = a.MatricolaInps != null && a.MatricolaInps != ""
                    && a.CodiceSedeInps != null && a.CodiceSedeInps != ""
                    && a.PatInail != null && a.PatInail != ""
                    && a.CcnlDefault != null && a.CcnlDefault != "",
            })
            .ToListAsync(ct);
        var aziendaIds = aziende.Select(a => a.Id).ToArray();

        var rapporti = await _db.Rapporti.AsNoTracking()
            .Where(r => r.TenantId == tenantId && aziendaIds.Contains(r.AziendaId)
                && r.Dipendente != null && r.Dipendente.TenantId == tenantId
                && r.DataAssunzione <= fine
                && (r.DataCessazione == null || r.DataCessazione >= inizio))
            .OrderBy(r => r.Dipendente!.Cognome).ThenBy(r => r.Dipendente!.Nome)
            .Select(r => new
            {
                r.Id,
                r.AziendaId,
                r.CodiceCcnl,
                Nominativo = (r.Dipendente!.Cognome + " " + r.Dipendente.Nome).Trim(),
            })
            .ToListAsync(ct);
        var rapportoIds = rapporti.Select(r => r.Id).ToArray();

        var cartellini = (await _db.Cartellini.AsNoTracking()
            .Where(c => c.TenantId == tenantId && c.Anno == anno && c.Mese == mese
                && rapportoIds.Contains(c.RapportoLavoroId))
            .Select(c => c.RapportoLavoroId)
            .ToListAsync(ct)).ToHashSet();
        var cedolini = await _db.Cedolini.AsNoTracking()
            .Where(c => c.TenantId == tenantId && c.Anno == anno && c.Mese == mese
                && rapportoIds.Contains(c.RapportoLavoroId))
            .Select(c => new { c.RapportoLavoroId, c.Stato })
            .ToDictionaryAsync(c => c.RapportoLavoroId, c => c.Stato, ct);
        var busteAgricole = (await _db.CedoliniAgricoli.AsNoTracking()
            .Where(c => c.TenantId == tenantId && c.Anno == anno && c.Mese == mese
                && rapportoIds.Contains(c.RapportoLavoroId))
            .Select(c => c.RapportoLavoroId)
            .ToListAsync(ct)).ToHashSet();

        var righePerAzienda = rapporti.ToLookup(r => r.AziendaId);
        var risultato = aziende.Select(azienda =>
        {
            var righe = righePerAzienda[azienda.Id].Select(r =>
            {
                cedolini.TryGetValue(r.Id, out var statoCedolino);
                var agricolo = string.Equals(
                    r.CodiceCcnl, "AGRICOLTURA_OPERAI", StringComparison.OrdinalIgnoreCase);
                var protetto = statoCedolino == nameof(StatoCedolino.PreWorkflow)
                    || statoCedolino == nameof(StatoCedolino.Controllata)
                    || statoCedolino == nameof(StatoCedolino.Validata)
                    || statoCedolino == nameof(StatoCedolino.Chiusa);
                string stato, motivo;
                if (agricolo)
                {
                    // Il ciclo agricolo non passa da cartellino/cedolino ordinari: conta
                    // l'archivio delle buste agricole del mese.
                    stato = busteAgricole.Contains(r.Id) ? "gia_elaborato" : "agricolo";
                    motivo = busteAgricole.Contains(r.Id)
                        ? "Busta agricola del mese archiviata."
                        : "Rapporto agricolo: si elabora dalla pagina Agricoltura OTD.";
                }
                else if (protetto)
                {
                    stato = "protetto";
                    motivo = "Cedolino controllato, validato, chiuso o storico: sola lettura.";
                }
                else if (statoCedolino is not null)
                {
                    stato = "gia_elaborato";
                    motivo = "Esiste già una bozza del cedolino.";
                }
                else if (!azienda.Completa)
                {
                    stato = "bloccato";
                    motivo = "Completare posizione INPS, PAT INAIL e CCNL dell'azienda.";
                }
                else if (!cartellini.Contains(r.Id))
                {
                    stato = "da_compilare";
                    motivo = "Cartellino del mese non ancora compilato.";
                }
                else
                {
                    stato = "cartellino_presente";
                    motivo = "Cartellino presente: pronto da elaborare.";
                }
                return new RigaCruscottoRapporto(r.Id, r.Nominativo, stato, motivo);
            }).ToList();

            return new AziendaCruscotto(
                azienda.Id,
                azienda.RagioneSociale,
                Elaborati: righe.Count(x => x.Stato is "gia_elaborato" or "protetto"),
                Pronti: righe.Count(x => x.Stato == "cartellino_presente"),
                DaCompilare: righe.Count(x => x.Stato == "da_compilare"),
                Bloccati: righe.Count(x => x.Stato == "bloccato"),
                Agricoli: righe.Count(x => x.Stato == "agricolo"),
                righe);
        }).ToList();

        return new CruscottoMese(
            anno, mese, aziendeTotali, aziendeTotali > aziende.Count, risultato);
    }
}
