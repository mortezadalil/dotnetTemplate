using Domain.Entities;
using Domain.Events;
using Infrastructure.Persistence;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.Common.Events;

/// <summary>
/// Handles ConfigCreatedEvent to synchronize new configs to the Query database.
/// This ensures the read model stays in sync with the write model.
/// </summary>
public class ConfigCreatedEventHandler : INotificationHandler<ConfigCreatedEvent>
{
    private readonly QueryDbContext _queryDb;
    private readonly ILogger<ConfigCreatedEventHandler> _logger;

    public ConfigCreatedEventHandler(
        QueryDbContext queryDb,
        ILogger<ConfigCreatedEventHandler> logger)
    {
        _queryDb = queryDb;
        _logger = logger;
    }

    public async Task Handle(ConfigCreatedEvent notification, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Syncing new config to Query DB: {Key}", notification.Key);

            // Create a new Config entity for the Query database
            var queryConfig = new Config();

            // Use reflection to set the private properties
            var configType = typeof(Config);

            configType.GetProperty("Id")!.SetValue(queryConfig, notification.ConfigId);
            configType.GetProperty("Key")!.SetValue(queryConfig, notification.Key);
            configType.GetProperty("Value")!.SetValue(queryConfig, notification.Value);
            configType.GetProperty("Description")!.SetValue(queryConfig, notification.Description);
            configType.GetProperty("Category")!.SetValue(queryConfig, notification.Category);
            configType.GetProperty("IsActive")!.SetValue(queryConfig, notification.IsActive);
            configType.GetProperty("CreatedAt")!.SetValue(queryConfig, notification.CreatedAt);
            configType.GetProperty("ModifiedAt")!.SetValue(queryConfig, notification.CreatedAt);

            await _queryDb.Configs.AddAsync(queryConfig, cancellationToken);
            await _queryDb.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Successfully synced config {Key} to Query DB", notification.Key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to sync config {Key} to Query DB. Manual sync may be required.",
                notification.Key);
            // In production, you should implement retry logic or dead letter queue
            // For now, don't throw - Command DB save already succeeded
        }
    }
}
