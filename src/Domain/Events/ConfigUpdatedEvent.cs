using Domain.Common;

namespace Domain.Events;

/// <summary>
/// Domain event raised when a configuration is updated.
/// Triggers synchronization to the read database and AppConfig refresh.
/// </summary>
public record ConfigUpdatedEvent : DomainEvent
{
    public Guid ConfigId { get; init; }
    public string Key { get; init; } = string.Empty;
    public string Value { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public bool IsActive { get; init; }
}
