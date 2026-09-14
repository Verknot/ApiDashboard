using ApiWorkbench.Api.Configuration;
using ApiWorkbench.Api.Services;

namespace ApiWorkbench.Api.Hosting;

public sealed class CatalogWatchHostedService(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<CatalogWatchHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        DateTime? lastWrite = null;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var path = ServicesYamlLocator.Resolve(configuration);
                if (File.Exists(path))
                {
                    var write = File.GetLastWriteTimeUtc(path);
                    if (lastWrite is not null && write > lastWrite)
                    {
                        logger.LogInformation("Обнаружено изменение {Path}, перечитываю каталог", path);
                        using var scope = scopeFactory.CreateScope();
                        var sync = scope.ServiceProvider.GetRequiredService<ICatalogSyncService>();
                        await sync.ReloadFromYamlAsync(stoppingToken);
                    }

                    lastWrite = write;
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Не удалось проверить services.yaml");
            }

            await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken);
        }
    }
}
