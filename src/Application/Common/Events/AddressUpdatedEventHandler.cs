using Domain.Events;
using Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Application.Common.Events;

/// <summary>
/// Handles AddressUpdatedEvent to synchronize address updates to the Query database.
/// This ensures the read model stays in sync with the write model.
/// </summary>
public class AddressUpdatedEventHandler : INotificationHandler<AddressUpdatedEvent>
{
    private readonly QueryDbContext _queryDb;
    private readonly ILogger<AddressUpdatedEventHandler> _logger;

    public AddressUpdatedEventHandler(
        QueryDbContext queryDb,
        ILogger<AddressUpdatedEventHandler> logger)
    {
        _queryDb = queryDb;
        _logger = logger;
    }

    public async Task Handle(AddressUpdatedEvent notification, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Syncing address update to Query DB: {AddressId}", notification.AddressId);

            var queryAddress = await _queryDb.Addresses
                .FirstOrDefaultAsync(a => a.Id == notification.AddressId, cancellationToken);

            if (queryAddress == null)
            {
                _logger.LogWarning("Address {AddressId} not found in Query DB for update", notification.AddressId);
                return;
            }

            // Update properties via reflection
            var addressType = typeof(Domain.Entities.Address);
            addressType.GetProperty("Street")!.SetValue(queryAddress, notification.Street);
            addressType.GetProperty("Street2")!.SetValue(queryAddress, notification.Street2);
            addressType.GetProperty("City")!.SetValue(queryAddress, notification.City);
            addressType.GetProperty("State")!.SetValue(queryAddress, notification.State);
            addressType.GetProperty("PostalCode")!.SetValue(queryAddress, notification.PostalCode);
            addressType.GetProperty("Country")!.SetValue(queryAddress, notification.Country);
            addressType.GetProperty("Label")!.SetValue(queryAddress, notification.Label);
            addressType.GetProperty("IsDefault")!.SetValue(queryAddress, notification.IsDefault);
            addressType.GetProperty("ModifiedAt")!.SetValue(queryAddress, notification.ModifiedAt);

            // Update phone numbers
            var updatePhonesMethod = addressType.GetMethod("UpdatePhones");
            updatePhonesMethod!.Invoke(queryAddress, new object[] { notification.PhoneNumbers });

            _queryDb.Addresses.Update(queryAddress);
            await _queryDb.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Successfully synced address update {AddressId} to Query DB", notification.AddressId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to sync address update {AddressId} to Query DB. Manual sync may be required.",
                notification.AddressId);
            // Don't throw - Command DB save already succeeded
            // Event is in outbox and will retry
        }
    }
}
