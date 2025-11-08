using Domain.Common;

namespace Domain.Events;

/// <summary>
/// Domain event raised when an address is updated.
/// Triggers synchronization to the read database.
/// </summary>
public record AddressUpdatedEvent : DomainEvent
{
    public Guid AddressId { get; init; }
    public Guid UserId { get; init; }
    public string Street { get; init; } = string.Empty;
    public string? Street2 { get; init; }
    public string City { get; init; } = string.Empty;
    public string State { get; init; } = string.Empty;
    public string PostalCode { get; init; } = string.Empty;
    public string Country { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public bool IsDefault { get; init; }
    public List<string> PhoneNumbers { get; init; } = new();
    public DateTime? ModifiedAt { get; init; }
}
