using Microsoft.EntityFrameworkCore;
using Paghe.Persistence.Entities;

namespace Paghe.Persistence;

/// <summary>
/// Contatore dei consumi AI per tenant/mese, per il modello "AI inclusa nel canone":
/// la chiave del provider è centrale, il cliente non configura nulla, e ogni studio ha
/// un limite mensile di letture AI. Superato il limite il software continua a funzionare
/// con la sola estrazione euristica (l'AI è un acceleratore, mai una dipendenza).
/// </summary>
public sealed class ConsumoAiService
{
    private readonly PagheDbContext _db;

    public ConsumoAiService(PagheDbContext db) => _db = db;

    /// <summary>
    /// Tenta di consumare una chiamata AI per il tenant nel mese corrente.
    /// True = sotto il limite, chiamata registrata; false = limite raggiunto, non chiamare l'AI.
    /// Limite &lt;= 0 = illimitato (viene comunque conteggiato).
    /// </summary>
    public async Task<bool> ProvaConsumaAsync(Guid tenantId, int limiteMensile, CancellationToken ct = default)
    {
        var oggi = DateTime.UtcNow;
        var riga = await _db.ConsumiAi.FirstOrDefaultAsync(
            c => c.TenantId == tenantId && c.Anno == oggi.Year && c.Mese == oggi.Month, ct);

        if (riga is null)
        {
            riga = new ConsumoAiEntity
            {
                Id = Guid.NewGuid(), TenantId = tenantId,
                Anno = oggi.Year, Mese = oggi.Month, Chiamate = 0,
            };
            _db.ConsumiAi.Add(riga);
        }

        if (limiteMensile > 0 && riga.Chiamate >= limiteMensile)
            return false;

        riga.Chiamate++;
        riga.AggiornatoIl = oggi;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>Chiamate AI del tenant nel mese corrente (0 se nessuna).</summary>
    public async Task<int> ConsumoDelMeseAsync(Guid tenantId, CancellationToken ct = default)
    {
        var oggi = DateTime.UtcNow;
        var riga = await _db.ConsumiAi.FirstOrDefaultAsync(
            c => c.TenantId == tenantId && c.Anno == oggi.Year && c.Mese == oggi.Month, ct);
        return riga?.Chiamate ?? 0;
    }
}
