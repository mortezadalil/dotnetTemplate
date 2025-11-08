using Domain.Common;

namespace Domain.Events;

/// <summary>
/// Domain event raised when a configuration is deleted (hard delete).
/// Triggers synchronization to the read database.
/// </summary>
public record ConfigDeletedEvent : DomainEvent
{
    public Guid ConfigId { get; init; }
}
