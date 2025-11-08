using Domain.Entities;
using Domain.Events;
using Infrastructure.Persistence;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.Common.Events;

/// <summary>
/// Handles UserCreatedEvent by synchronizing the user to the Query database.
/// This enables CQRS with separate read/write databases.
/// </summary>
public class UserCreatedEventHandler : INotificationHandler<UserCreatedEvent>
{
    private readonly QueryDbContext _queryDb;
    private readonly ILogger<UserCreatedEventHandler> _logger;

    public UserCreatedEventHandler(
        QueryDbContext queryDb,
        ILogger<UserCreatedEventHandler> logger)
    {
        _queryDb = queryDb;
        _logger = logger;
    }

    public async Task Handle(UserCreatedEvent notification, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation(
                "Synchronizing new user to Query DB: {UserId} - {Email}",
                notification.UserId,
                notification.Email);

            // Create user in Query DB
            // Note: We use EF Core's Add directly here since this is sync, not domain logic
            var queryUser = new User();

            // Use reflection to set properties (since User has private setters)
            typeof(User).GetProperty(nameof(User.Id))!.SetValue(queryUser, notification.UserId);
            typeof(User).GetProperty(nameof(BaseEntity.CreatedAt))!.SetValue(queryUser, notification.CreatedAt);

            // Set via reflection for private setters
            var emailProp = typeof(User).GetProperty("Email")!;
            var fullNameProp = typeof(User).GetProperty("FullName")!;
            var roleProp = typeof(User).GetProperty("Role")!;
            var isEmailVerifiedProp = typeof(User).GetProperty("IsEmailVerified")!;
            var isActiveProp = typeof(User).GetProperty("IsActive")!;

            emailProp.SetValue(queryUser, notification.Email);
            fullNameProp.SetValue(queryUser, notification.FullName);
            roleProp.SetValue(queryUser, notification.Role);
            isEmailVerifiedProp.SetValue(queryUser, notification.IsEmailVerified);
            isActiveProp.SetValue(queryUser, notification.IsActive);

            await _queryDb.Users.AddAsync(queryUser, cancellationToken);
            await _queryDb.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Successfully synchronized user {UserId} to Query DB",
                notification.UserId);
        }
        catch (Exception ex)
        {
            // Log error but don't throw - Command DB save already succeeded
            // In production, you might want to queue this for retry
            _logger.LogError(ex,
                "Failed to synchronize user {UserId} to Query DB. Manual sync may be required.",
                notification.UserId);
        }
    }
}
