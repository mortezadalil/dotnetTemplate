using Domain.Common;

namespace Domain.Events;

/// <summary>
/// Domain event raised when a user is deleted (soft delete).
/// Triggers synchronization to the read database.
/// </summary>
public record UserDeletedEvent : DomainEvent
{
    public Guid UserId { get; init; }
}
