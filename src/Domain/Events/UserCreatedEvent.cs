using Domain.Common;
using Domain.Enums;

namespace Domain.Events;

/// <summary>
/// Domain event raised when a new user is created.
/// Triggers synchronization to the read database.
/// </summary>
public record UserCreatedEvent : DomainEvent
{
    public Guid UserId { get; init; }
    public string Email { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public UserRole Role { get; init; }
    public bool IsEmailVerified { get; init; }
    public bool IsActive { get; init; }
    public DateTime CreatedAt { get; init; }
}
