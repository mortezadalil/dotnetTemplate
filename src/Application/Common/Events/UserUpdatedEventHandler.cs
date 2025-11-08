using Domain.Events;
using Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Application.Common.Events;

/// <summary>
/// Handles UserUpdatedEvent by synchronizing changes to the Query database.
/// </summary>
public class UserUpdatedEventHandler : INotificationHandler<UserUpdatedEvent>
{
    private readonly QueryDbContext _queryDb;
    private readonly ILogger<UserUpdatedEventHandler> _logger;

    public UserUpdatedEventHandler(
        QueryDbContext queryDb,
        ILogger<UserUpdatedEventHandler> logger)
    {
        _queryDb = queryDb;
        _logger = logger;
    }

    public async Task Handle(UserUpdatedEvent notification, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation(
                "Synchronizing user update to Query DB: {UserId}",
                notification.UserId);

            // Find user in Query DB
            var queryUser = await _queryDb.Users
                .FirstOrDefaultAsync(u => u.Id == notification.UserId, cancellationToken);

            if (queryUser == null)
            {
                _logger.LogWarning(
                    "User {UserId} not found in Query DB. Cannot sync update.",
                    notification.UserId);
                return;
            }

            // Update properties via reflection (due to private setters)
            var emailProp = typeof(Domain.Entities.User).GetProperty("Email")!;
            var fullNameProp = typeof(Domain.Entities.User).GetProperty("FullName")!;
            var roleProp = typeof(Domain.Entities.User).GetProperty("Role")!;
            var isEmailVerifiedProp = typeof(Domain.Entities.User).GetProperty("IsEmailVerified")!;
            var isActiveProp = typeof(Domain.Entities.User).GetProperty("IsActive")!;
            var lastLoginProp = typeof(Domain.Entities.User).GetProperty("LastLoginAt")!;
            var modifiedAtProp = typeof(Domain.Entities.User).GetProperty(nameof(Domain.Common.BaseEntity.ModifiedAt))!;

            emailProp.SetValue(queryUser, notification.Email);
            fullNameProp.SetValue(queryUser, notification.FullName);
            roleProp.SetValue(queryUser, notification.Role);
            isEmailVerifiedProp.SetValue(queryUser, notification.IsEmailVerified);
            isActiveProp.SetValue(queryUser, notification.IsActive);
            lastLoginProp.SetValue(queryUser, notification.LastLoginAt);
            modifiedAtProp.SetValue(queryUser, notification.ModifiedAt);

            await _queryDb.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Successfully synchronized user update {UserId} to Query DB",
                notification.UserId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to synchronize user update {UserId} to Query DB",
                notification.UserId);
        }
    }
}
