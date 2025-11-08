using Domain.Events;
using Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Application.Common.Events;

/// <summary>
/// Handles ConfigUpdatedEvent to synchronize config updates to the Query database.
/// This ensures the read model stays in sync with the write model.
/// </summary>
public class ConfigUpdatedEventHandler : INotificationHandler<ConfigUpdatedEvent>
{
    private readonly QueryDbContext _queryDb;
    private readonly ILogger<ConfigUpdatedEventHandler> _logger;

    public ConfigUpdatedEventHandler(
        QueryDbContext queryDb,
        ILogger<ConfigUpdatedEventHandler> logger)
    {
        _queryDb = queryDb;
        _logger = logger;
    }

    public async Task Handle(ConfigUpdatedEvent notification, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Syncing config update to Query DB: {Key}", notification.Key);

            var queryConfig = await _queryDb.Configs
                .FirstOrDefaultAsync(c => c.Id == notification.ConfigId, cancellationToken);

            if (queryConfig == null)
            {
                _logger.LogWarning("Config {ConfigId} not found in Query DB for update", notification.ConfigId);
                return;
            }

            // Use reflection to update the private properties
            var configType = typeof(Domain.Entities.Config);

            configType.GetProperty("Key")!.SetValue(queryConfig, notification.Key);
            configType.GetProperty("Value")!.SetValue(queryConfig, notification.Value);
            configType.GetProperty("Description")!.SetValue(queryConfig, notification.Description);
            configType.GetProperty("Category")!.SetValue(queryConfig, notification.Category);
            configType.GetProperty("IsActive")!.SetValue(queryConfig, notification.IsActive);
            configType.GetProperty("ModifiedAt")!.SetValue(queryConfig, DateTime.UtcNow);

            _queryDb.Configs.Update(queryConfig);
            await _queryDb.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Successfully synced config update {Key} to Query DB", notification.Key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to sync config update {Key} to Query DB. Manual sync may be required.",
                notification.Key);
            // In production, you should implement retry logic or dead letter queue
            // For now, don't throw - Command DB save already succeeded
        }
    }
}
