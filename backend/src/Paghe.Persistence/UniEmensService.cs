using Microsoft.EntityFrameworkCore;
using Paghe.Core.AdeTelematico.UniEmens;
using Paghe.Core.Domain;
using Paghe.Persistence.Cedolini;

namespace Paghe.Persistence;

/// <summary>
/// Costruisce il flusso UniEmens (denuncia mensile INPS) per un'azienda raccogliendo i
/// cedolini archiviati del periodo e l'anagrafica dei lavoratori. Prepara solo il modello/XML:
/// la validazione col software di controllo ufficiale INPS e l'invio telematico restano
/// demandati ai canali ufficiali (cassetto previdenziale / intermediario). Isolato per tenant.
/// </summary>
public sealed class UniEmensService
{
    /// <summary>Giorni retribuiti di default: da affinare con i dati di presenza del cartellino.</summary>
    private const int GiorniRetribuitiDefault = 26;

    private readonly PagheDbContext _db;

    public UniEmensService(PagheDbContext db) => _db = db;

    public async Task<FlussoUniEmens> GeneraDelMeseAsync(
        Guid tenantId, Guid aziendaId, int anno, int mese,
        string matricolaInps, CancellationToken ct = default)
    {
        var azienda = await _db.Aziende
            .FirstOrDefaultAsync(a => a.Id == aziendaId && a.TenantId == tenantId, ct)
            ?? throw new ArgumentException("Azienda non trovata per il tenant indicato.");
        AziendeService.VerificaProntaPerAdempimentiContributivi(azienda);

        var cedolini = await _db.Cedolini.UtilizzabiliAValle()
            .Where(c => c.TenantId == tenantId && c.AziendaId == aziendaId
                && c.Anno == anno && c.Mese == mese)
            .ToListAsync(ct);
        if (cedolini.Count == 0)
            throw new ArgumentException(
                $"Nessun cedolino chiuso o storico pre-workflow per il periodo {mese:00}/{anno}.");

        // Anagrafica lavoratori: rapporto -> dipendente (codice fiscale, cognome, nome, qualifica).
        var rapportoIds = cedolini.Select(c => c.RapportoLavoroId).Distinct().ToList();
        var rapporti = await _db.Rapporti
            .Include(r => r.Dipendente)
            .Where(r => r.TenantId == tenantId && rapportoIds.Contains(r.Id))
            .ToDictionaryAsync(r => r.Id, ct);

        var righe = cedolini.Select(c =>
        {
            rapporti.TryGetValue(c.RapportoLavoroId, out var rap);
            var dip = rap?.Dipendente;
            return new RigaUniEmens(
                CodiceFiscale: dip?.CodiceFiscale ?? "",
                Cognome: dip?.Cognome ?? "",
                Nome: dip?.Nome ?? "",
                // Qualifica enum interna ("Impiegato"/"Operaio"…): va mappata al codice UniEmens ufficiale.
                Qualifica: rap?.Qualifica ?? "",
                ImponibileInps: c.ImponibileInps,
                ContributiDipendente: c.ContributiDipendente,
                ContributiDatore: c.ContributiDatore,
                GiorniRetribuiti: GiorniRetribuitiDefault);
        }).ToList();

        var mittente = new DatiMittenteUniEmens(
            azienda.CodiceFiscale ?? azienda.PartitaIva ?? "", azienda.RagioneSociale);
        var denunciaAziendale = new DenunciaAziendaleUniEmens(
            azienda.MatricolaInps!, azienda.CodiceFiscale ?? azienda.PartitaIva ?? "", anno, mese);

        return UniEmensDaCedolini.Costruisci(mittente, denunciaAziendale, righe);
    }
}
