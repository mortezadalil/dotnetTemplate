using Domain.Events;
using Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Application.Common.Events;

/// <summary>
/// Handles AddressDeletedEvent to synchronize address deletions to the Query database.
/// This ensures the read model stays in sync with the write model.
/// </summary>
public class AddressDeletedEventHandler : INotificationHandler<AddressDeletedEvent>
{
    private readonly QueryDbContext _queryDb;
    private readonly ILogger<AddressDeletedEventHandler> _logger;

    public AddressDeletedEventHandler(
        QueryDbContext queryDb,
        ILogger<AddressDeletedEventHandler> logger)
    {
        _queryDb = queryDb;
        _logger = logger;
    }

    public async Task Handle(AddressDeletedEvent notification, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Syncing address deletion to Query DB: {AddressId}", notification.AddressId);

            var queryAddress = await _queryDb.Addresses
                .FirstOrDefaultAsync(a => a.Id == notification.AddressId, cancellationToken);

            if (queryAddress == null)
            {
                _logger.LogWarning("Address {AddressId} not found in Query DB for deletion", notification.AddressId);
                return;
            }

            // Soft delete in Query DB
            typeof(Domain.Common.BaseEntity).GetProperty("IsDeleted")!.SetValue(queryAddress, true);
            typeof(Domain.Common.BaseEntity).GetProperty("ModifiedAt")!.SetValue(queryAddress, DateTime.UtcNow);

            _queryDb.Addresses.Update(queryAddress);
            await _queryDb.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Successfully synced address deletion {AddressId} to Query DB", notification.AddressId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to sync address deletion {AddressId} to Query DB. Manual sync may be required.",
                notification.AddressId);
            // Don't throw - Command DB save already succeeded
            // Event is in outbox and will retry
        }
    }
}
