using Paghe.Persistence.Sicurezza;

namespace Paghe.Api;

/// <summary>Pulizia giornaliera del solo materiale tecnico delle sessioni scadute.</summary>
public sealed class PuliziaSessioniRefreshWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<PuliziaSessioniRefreshWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var eliminate = await scope.ServiceProvider.GetRequiredService<SessioniRefreshService>()
                    .EliminaScaduteAsync(stoppingToken);
                if (eliminate > 0)
                    logger.LogInformation("Eliminate {Numero} righe tecniche di sessioni scadute.", eliminate);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Pulizia delle sessioni refresh non riuscita.");
            }

            await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
        }
    }
}
