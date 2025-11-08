using MediatR;

namespace Domain.Common;

/// <summary>
/// Base interface for all domain events.
/// Domain events represent something that happened in the domain.
/// </summary>
public interface IDomainEvent : INotification
{
    DateTime OccurredOn { get; }
}
