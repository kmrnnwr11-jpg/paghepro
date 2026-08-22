using Microsoft.EntityFrameworkCore;
using Paghe.Core.Domain;
using Paghe.Persistence;

namespace Paghe.Tests;

/// <summary>
/// Costruisce nei test uno storico anteriore al workflow senza forgiare una chiusura moderna
/// priva di snapshot, validazione e PDF. Il SQL è confinato alle fixture e sempre tenant-scoped.
/// </summary>
internal static class CedolinoFixtureStati
{
    internal static int ClassificaPreWorkflow(
        this PagheDbContext db,
        Guid tenantId,
        Guid cedolinoId) =>
        db.Database.ExecuteSqlInterpolated($"""
            UPDATE Cedolini
            SET Stato = {nameof(StatoCedolino.PreWorkflow)},
                Versione = 1,
                ParametriApplicatiProtetti = '',
                HashParametriSha256 = ''
            WHERE TenantId = {tenantId} AND Id = {cedolinoId}
            """);

    internal static Task ClassificaPreWorkflowAsync(
        this PagheDbContext db,
        Guid tenantId,
        Guid cedolinoId) =>
        db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE Cedolini
            SET Stato = {nameof(StatoCedolino.PreWorkflow)},
                Versione = 1,
                ParametriApplicatiProtetti = '',
                HashParametriSha256 = ''
            WHERE TenantId = {tenantId} AND Id = {cedolinoId}
            """);
}
