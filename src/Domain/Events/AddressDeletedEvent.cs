using Domain.Common;

namespace Domain.Events;

/// <summary>
/// Domain event raised when an address is deleted (soft delete).
/// Triggers synchronization to the read database.
/// </summary>
public record AddressDeletedEvent : DomainEvent
{
    public Guid AddressId { get; init; }
    public Guid UserId { get; init; }
}
