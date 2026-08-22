using Microsoft.EntityFrameworkCore;
using Paghe.Persistence.Entities;

namespace Paghe.Persistence;

/// <summary>Un livello di un CCNL da caricare (minimi tabellari).</summary>
public sealed record LivelloCcnlInput(
    string Livello, string Descrizione, decimal MinimoTabellare, decimal Edr, decimal ImportoScatto);

/// <summary>Un CCNL da caricare con i suoi livelli, per un dato anno.</summary>
public sealed record CcnlInput(
    string Codice,
    string Descrizione,
    int Anno,
    List<LivelloCcnlInput> Livelli,
    string? CodiceCnel = null,
    string? Fonte = null);

/// <summary>
/// Carica/aggiorna i CCNL e i loro minimi tabellari da dati esterni (file/JSON), così da poter
/// avere <b>qualsiasi CCNL</b> con i valori <b>reali</b> senza modificare il codice. È un upsert per
/// (codice + anno): i livelli dell'anno vengono sostituiti con quelli forniti.
/// </summary>
public sealed class CcnlImportService
{
    private readonly PagheDbContext _db;

    public CcnlImportService(PagheDbContext db) => _db = db;

    public async Task<(int ccnl, int livelli)> ImportaAsync(
        IEnumerable<CcnlInput> input, CancellationToken ct = default)
    {
        int nCcnl = 0, nLiv = 0;
        foreach (var c in input)
        {
            if (string.IsNullOrWhiteSpace(c.Codice))
                continue;

            var esistente = await _db.Ccnl.FirstOrDefaultAsync(x => x.Codice == c.Codice, ct);
            if (esistente is null)
                _db.Ccnl.Add(new CcnlEntity { Codice = c.Codice, Descrizione = c.Descrizione });
            else
                esistente.Descrizione = c.Descrizione;

            var versione = await _db.CcnlVersioni
                .FirstOrDefaultAsync(v => v.CcnlCodice == c.Codice && v.Anno == c.Anno, ct);
            if (versione is null)
            {
                versione = new CcnlVersioneEntity
                {
                    Id = Guid.NewGuid(), CcnlCodice = c.Codice, Anno = c.Anno,
                };
                _db.CcnlVersioni.Add(versione);
            }
            versione.CodiceCnel = string.IsNullOrWhiteSpace(c.CodiceCnel)
                ? null
                : c.CodiceCnel.Trim().ToUpperInvariant();
            versione.Fonte = string.IsNullOrWhiteSpace(c.Fonte) ? null : c.Fonte.Trim();

            // Sostituisce i livelli di quell'anno con quelli forniti (upsert).
            var vecchi = await _db.CcnlLivelli
                .Where(l => l.CcnlCodice == c.Codice && l.Anno == c.Anno).ToListAsync(ct);
            _db.CcnlLivelli.RemoveRange(vecchi);

            foreach (var l in c.Livelli ?? [])
            {
                _db.CcnlLivelli.Add(new CcnlLivelloEntity
                {
                    Id = Guid.NewGuid(), CcnlCodice = c.Codice, Anno = c.Anno,
                    Livello = l.Livello, Descrizione = l.Descrizione,
                    MinimoTabellare = l.MinimoTabellare, Edr = l.Edr, ImportoScatto = l.ImportoScatto,
                });
                nLiv++;
            }
            nCcnl++;
        }

        await _db.SaveChangesAsync(ct);
        return (nCcnl, nLiv);
    }
}
