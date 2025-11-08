using Domain.Events;
using Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Application.Common.Events;

/// <summary>
/// Handles ConfigDeletedEvent to synchronize config deletions to the Query database.
/// This ensures the read model stays in sync with the write model.
/// </summary>
public class ConfigDeletedEventHandler : INotificationHandler<ConfigDeletedEvent>
{
    private readonly QueryDbContext _queryDb;
    private readonly ILogger<ConfigDeletedEventHandler> _logger;

    public ConfigDeletedEventHandler(
        QueryDbContext queryDb,
        ILogger<ConfigDeletedEventHandler> logger)
    {
        _queryDb = queryDb;
        _logger = logger;
    }

    public async Task Handle(ConfigDeletedEvent notification, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Syncing config deletion to Query DB: {ConfigId}", notification.ConfigId);

            var queryConfig = await _queryDb.Configs
                .FirstOrDefaultAsync(c => c.Id == notification.ConfigId, cancellationToken);

            if (queryConfig == null)
            {
                _logger.LogWarning("Config {ConfigId} not found in Query DB for deletion", notification.ConfigId);
                return;
            }

            _queryDb.Configs.Remove(queryConfig);
            await _queryDb.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Successfully synced config deletion {ConfigId} to Query DB", notification.ConfigId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to sync config deletion {ConfigId} to Query DB. Manual sync may be required.",
                notification.ConfigId);
            // In production, you should implement retry logic or dead letter queue
            // For now, don't throw - Command DB save already succeeded
        }
    }
}
