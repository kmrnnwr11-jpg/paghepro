using Microsoft.EntityFrameworkCore;

namespace Paghe.Persistence.Cedolini;

/// <summary>
/// Ricava dal cartellino precedente il riporto autorevole della settimana che attraversa
/// il cambio mese. La query è sempre limitata a tenant e rapporto: il valore dichiarato dal
/// client non può auto-certificarsi.
/// </summary>
public static class RiportoOreSettimanaliService
{
    public static async Task<decimal?> CalcolaAsync(
        PagheDbContext db,
        Guid tenantId,
        Guid rapportoId,
        int anno,
        int mese,
        DateOnly? dataAssunzione = null,
        CancellationToken ct = default)
    {
        var primoGiorno = new DateOnly(anno, mese, 1);
        int distanzaDaLunedi = ((int)primoGiorno.DayOfWeek + 6) % 7;
        if (distanzaDaLunedi == 0)
            return 0m;

        var lunedi = primoGiorno.AddDays(-distanzaDaLunedi);
        var dateDaVerificare = Enumerable.Range(0, distanzaDaLunedi)
            .Select(offset => lunedi.AddDays(offset))
            .Where(data => dataAssunzione is null || data >= dataAssunzione.Value)
            .ToList();
        if (dateDaVerificare.Count == 0)
            return 0m;

        var periodoPrecedente = primoGiorno.AddDays(-1);
        var precedente = await db.Cartellini.AsNoTracking().FirstOrDefaultAsync(c =>
            c.TenantId == tenantId
            && c.RapportoLavoroId == rapportoId
            && c.Anno == periodoPrecedente.Year
            && c.Mese == periodoPrecedente.Month, ct);
        if (precedente is null)
            return null;

        var perData = precedente.Giornate.ToDictionary(g => g.Data);
        if (dateDaVerificare.Any(data => !perData.ContainsKey(data)))
            return null;

        return dateDaVerificare.Sum(data => perData[data].TotaleOreLavorate);
    }
}
