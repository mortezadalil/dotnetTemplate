namespace Domain.Common;

/// <summary>
/// Base class for domain events.
/// </summary>
public abstract record DomainEvent : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}
