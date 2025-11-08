using Domain.Events;
using Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Application.Common.Events;

/// <summary>
/// Handles UserDeletedEvent by synchronizing soft delete to the Query database.
/// </summary>
public class UserDeletedEventHandler : INotificationHandler<UserDeletedEvent>
{
    private readonly QueryDbContext _queryDb;
    private readonly ILogger<UserDeletedEventHandler> _logger;

    public UserDeletedEventHandler(
        QueryDbContext queryDb,
        ILogger<UserDeletedEventHandler> logger)
    {
        _queryDb = queryDb;
        _logger = logger;
    }

    public async Task Handle(UserDeletedEvent notification, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation(
                "Synchronizing user deletion to Query DB: {UserId}",
                notification.UserId);

            var queryUser = await _queryDb.Users
                .IgnoreQueryFilters() // Include soft-deleted records
                .FirstOrDefaultAsync(u => u.Id == notification.UserId, cancellationToken);

            if (queryUser == null)
            {
                _logger.LogWarning(
                    "User {UserId} not found in Query DB. Cannot sync deletion.",
                    notification.UserId);
                return;
            }

            // Soft delete in Query DB
            var isDeletedProp = typeof(Domain.Common.BaseEntity).GetProperty(nameof(Domain.Common.BaseEntity.IsDeleted))!;
            var modifiedAtProp = typeof(Domain.Common.BaseEntity).GetProperty(nameof(Domain.Common.BaseEntity.ModifiedAt))!;

            isDeletedProp.SetValue(queryUser, true);
            modifiedAtProp.SetValue(queryUser, DateTime.UtcNow);

            await _queryDb.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Successfully synchronized user deletion {UserId} to Query DB",
                notification.UserId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to synchronize user deletion {UserId} to Query DB",
                notification.UserId);
        }
    }
}
