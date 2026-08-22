using Paghe.Core.Domain;
using Paghe.Persistence.Entities;

namespace Paghe.Persistence.Cedolini;

/// <summary>
/// Criteri condivisi per i cedolini che possono alimentare adempimenti e progressivi.
/// I record preesistenti al workflow restano utilizzabili senza attribuire loro una chiusura
/// mai avvenuta; bozze, controllati e validati non devono invece alterare dati definitivi.
/// </summary>
public static class CedolinoStatiQuery
{
    /// <summary>
    /// Mantiene esclusivamente cedolini chiusi nel workflow oppure storici pre-workflow.
    /// Il predicato resta nell'albero LINQ ed è quindi traducibile da Entity Framework.
    /// </summary>
    public static IQueryable<CedolinoEntity> UtilizzabiliAValle(
        this IQueryable<CedolinoEntity> query) =>
        query.Where(c =>
            c.Stato == nameof(StatoCedolino.Chiusa)
            || c.Stato == nameof(StatoCedolino.PreWorkflow));
}
