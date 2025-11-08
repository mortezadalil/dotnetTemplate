using Application.Common.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Configuration;

/// <summary>
/// Background service that refreshes AppConfig from the database every 1 minute.
/// This allows configuration changes in the database to be picked up without restarting the app.
/// </summary>
public class AppConfigRefreshService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AppConfigRefreshService> _logger;
    private readonly TimeSpan _refreshInterval = TimeSpan.FromMinutes(1);

    public AppConfigRefreshService(
        IServiceProvider serviceProvider,
        ILogger<AppConfigRefreshService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AppConfig Refresh Service started. Refresh interval: {Interval}", _refreshInterval);

        // Initial load
        await RefreshConfigsAsync(stoppingToken);

        // Periodic refresh
        using var timer = new PeriodicTimer(_refreshInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await timer.WaitForNextTickAsync(stoppingToken);
                await RefreshConfigsAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Expected when stopping
                _logger.LogInformation("AppConfig Refresh Service is stopping");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in AppConfig Refresh Service");
            }
        }
    }

    private async Task RefreshConfigsAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Create a new scope to get scoped services
            using var scope = _serviceProvider.CreateScope();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            // Load all active configs from database
            var configs = await unitOfWork.Configs.GetAllAsync(
                c => c.IsActive,
                cancellationToken);

            // Convert to dictionary
            var configDictionary = configs.ToDictionary(c => c.Key, c => c.Value);

            // Refresh AppConfig
            AppConfig.RefreshDatabaseConfigs(configDictionary);
            AppConfig.LastRefreshTime = DateTime.UtcNow;

            _logger.LogInformation(
                "AppConfig refreshed successfully. Loaded {Count} configurations",
                configDictionary.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh AppConfig from database");
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("AppConfig Refresh Service is stopping");
        await base.StopAsync(cancellationToken);
    }
}
