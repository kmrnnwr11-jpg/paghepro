using Paghe.Persistence.Ai;
using Microsoft.EntityFrameworkCore;
using Paghe.Persistence;

namespace Paghe.Api;

/// <summary>
/// Elimina in autonomia i transcript Aurora oltre il TTL. Le API li rendono comunque
/// irrecuperabili allo scadere esatto; questo worker chiude anche la cancellazione fisica
/// quando l'utente non torna più nell'applicazione.
/// </summary>
public sealed class PuliziaConversazioniAuroraWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<PuliziaConversazioniAuroraWorker> logger) : BackgroundService
{
    private const int DimensioneLotto = 500;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var eliminateTotali = 0;
                await using var inventarioScope = scopeFactory.CreateAsyncScope();
                var tenantIds = await inventarioScope.ServiceProvider
                    .GetRequiredService<PagheDbContext>()
                    .Tenants.AsNoTracking()
                    .OrderBy(t => t.Id)
                    .Select(t => t.Id)
                    .ToListAsync(stoppingToken);
                foreach (var tenantId in tenantIds)
                {
                    int eliminate;
                    do
                    {
                        await using var scope = scopeFactory.CreateAsyncScope();
                        eliminate = await scope.ServiceProvider
                            .GetRequiredService<AuroraConversazioniService>()
                            .EliminaScaduteTenantAsync(
                                tenantId, DimensioneLotto, stoppingToken);
                        eliminateTotali += eliminate;
                    }
                    while (eliminate == DimensioneLotto
                           && !stoppingToken.IsCancellationRequested);
                }

                if (eliminateTotali > 0)
                    logger.LogInformation(
                        "Eliminate {Numero} conversazioni Aurora oltre il TTL.", eliminateTotali);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Pulizia delle conversazioni Aurora non riuscita.");
            }

            // Il TTL logico è applicato allo scadere esatto dalle query; il ciclo breve limita
            // a pochi minuti la permanenza fisica di righe già irrecuperabili.
            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        }
    }
}
