using Domain.Entities;
using Domain.Events;
using Infrastructure.Persistence;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.Common.Events;

/// <summary>
/// Handles AddressCreatedEvent to synchronize new addresses to the Query database.
/// This ensures the read model stays in sync with the write model.
/// </summary>
public class AddressCreatedEventHandler : INotificationHandler<AddressCreatedEvent>
{
    private readonly QueryDbContext _queryDb;
    private readonly ILogger<AddressCreatedEventHandler> _logger;

    public AddressCreatedEventHandler(
        QueryDbContext queryDb,
        ILogger<AddressCreatedEventHandler> logger)
    {
        _queryDb = queryDb;
        _logger = logger;
    }

    public async Task Handle(AddressCreatedEvent notification, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Syncing new address to Query DB: {AddressId} for User: {UserId}",
                notification.AddressId, notification.UserId);

            // Create address in Query DB
            var queryAddress = new Address();

            // Use reflection to set properties
            var addressType = typeof(Address);
            addressType.GetProperty("Id")!.SetValue(queryAddress, notification.AddressId);
            addressType.GetProperty("UserId")!.SetValue(queryAddress, notification.UserId);
            addressType.GetProperty("Street")!.SetValue(queryAddress, notification.Street);
            addressType.GetProperty("Street2")!.SetValue(queryAddress, notification.Street2);
            addressType.GetProperty("City")!.SetValue(queryAddress, notification.City);
            addressType.GetProperty("State")!.SetValue(queryAddress, notification.State);
            addressType.GetProperty("PostalCode")!.SetValue(queryAddress, notification.PostalCode);
            addressType.GetProperty("Country")!.SetValue(queryAddress, notification.Country);
            addressType.GetProperty("Label")!.SetValue(queryAddress, notification.Label);
            addressType.GetProperty("IsDefault")!.SetValue(queryAddress, notification.IsDefault);
            addressType.GetProperty("CreatedAt")!.SetValue(queryAddress, notification.CreatedAt);
            addressType.GetProperty("ModifiedAt")!.SetValue(queryAddress, notification.CreatedAt);

            // Add phone numbers
            if (notification.PhoneNumbers.Any())
            {
                var updatePhonesMethod = addressType.GetMethod("UpdatePhones");
                updatePhonesMethod!.Invoke(queryAddress, new object[] { notification.PhoneNumbers });
            }

            await _queryDb.Addresses.AddAsync(queryAddress, cancellationToken);
            await _queryDb.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Successfully synced address {AddressId} to Query DB", notification.AddressId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to sync address {AddressId} to Query DB. Manual sync may be required.",
                notification.AddressId);
            // Don't throw - Command DB save already succeeded
            // Event is in outbox and will retry
        }
    }
}
