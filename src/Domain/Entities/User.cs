using Domain.Common;
using Domain.Enums;
using Domain.Events;

namespace Domain.Entities;

/// <summary>
/// Represents a user in the system.
/// This is a pure domain entity - no annotations, no infrastructure concerns.
/// Raises domain events for CQRS synchronization.
/// </summary>
public class User : BaseEntity
{
    /// <summary>
    /// User's email address. Must be unique across the system.
    /// </summary>
    public string Email { get; private set; } = string.Empty;

    /// <summary>
    /// User's full name.
    /// </summary>
    public string FullName { get; private set; } = string.Empty;

    /// <summary>
    /// Hashed password. Never store plain text passwords.
    /// </summary>
    public string PasswordHash { get; private set; } = string.Empty;

    /// <summary>
    /// User's role in the system (User or Admin).
    /// </summary>
    public UserRole Role { get; private set; }

    /// <summary>
    /// Whether the user's email has been verified.
    /// </summary>
    public bool IsEmailVerified { get; private set; }

    /// <summary>
    /// Whether the user account is active.
    /// </summary>
    public bool IsActive { get; private set; }

    /// <summary>
    /// Last time the user logged in.
    /// </summary>
    public DateTime? LastLoginAt { get; private set; }

    /// <summary>
    /// Private constructor for EF Core.
    /// </summary>
    private User() { }

    /// <summary>
    /// Creates a new user. This is the only way to create a valid user.
    /// Raises UserCreatedEvent for CQRS synchronization.
    /// </summary>
    public static User Create(string email, string fullName, string passwordHash, UserRole role = UserRole.User)
    {
        // Domain validation could go here
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email cannot be empty", nameof(email));

        if (string.IsNullOrWhiteSpace(fullName))
            throw new ArgumentException("Full name cannot be empty", nameof(fullName));

        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new ArgumentException("Password hash cannot be empty", nameof(passwordHash));

        var user = new User
        {
            Email = email.ToLowerInvariant(),
            FullName = fullName,
            PasswordHash = passwordHash,
            Role = role,
            IsEmailVerified = false,
            IsActive = true
        };

        // Raise domain event for synchronization to read database
        user.AddDomainEvent(new UserCreatedEvent
        {
            UserId = user.Id,
            Email = user.Email,
            FullName = user.FullName,
            Role = user.Role,
            IsEmailVerified = user.IsEmailVerified,
            IsActive = user.IsActive,
            CreatedAt = user.CreatedAt
        });

        return user;
    }

    /// <summary>
    /// Updates the user's password.
    /// Raises UserUpdatedEvent for CQRS synchronization.
    /// </summary>
    public void UpdatePassword(string newPasswordHash)
    {
        if (string.IsNullOrWhiteSpace(newPasswordHash))
            throw new ArgumentException("Password hash cannot be empty", nameof(newPasswordHash));

        PasswordHash = newPasswordHash;
        ModifiedAt = DateTime.UtcNow;

        RaiseUpdatedEvent();
    }

    /// <summary>
    /// Marks the email as verified.
    /// Raises UserUpdatedEvent for CQRS synchronization.
    /// </summary>
    public void VerifyEmail()
    {
        IsEmailVerified = true;
        ModifiedAt = DateTime.UtcNow;

        RaiseUpdatedEvent();
    }

    /// <summary>
    /// Records a successful login.
    /// Raises UserUpdatedEvent for CQRS synchronization.
    /// </summary>
    public void RecordLogin()
    {
        LastLoginAt = DateTime.UtcNow;
        ModifiedAt = DateTime.UtcNow;

        RaiseUpdatedEvent();
    }

    /// <summary>
    /// Deactivates the user account.
    /// Raises UserUpdatedEvent for CQRS synchronization.
    /// </summary>
    public void Deactivate()
    {
        IsActive = false;
        ModifiedAt = DateTime.UtcNow;

        RaiseUpdatedEvent();
    }

    /// <summary>
    /// Activates the user account.
    /// Raises UserUpdatedEvent for CQRS synchronization.
    /// </summary>
    public void Activate()
    {
        IsActive = true;
        ModifiedAt = DateTime.UtcNow;

        RaiseUpdatedEvent();
    }

    /// <summary>
    /// Changes the user's role.
    /// Raises UserUpdatedEvent for CQRS synchronization.
    /// </summary>
    public void ChangeRole(UserRole newRole)
    {
        Role = newRole;
        ModifiedAt = DateTime.UtcNow;

        RaiseUpdatedEvent();
    }

    /// <summary>
    /// Promotes the user to Admin role.
    /// Raises UserUpdatedEvent for CQRS synchronization.
    /// </summary>
    public void PromoteToAdmin()
    {
        ChangeRole(UserRole.Admin);
    }

    /// <summary>
    /// Demotes the user to regular User role.
    /// Raises UserUpdatedEvent for CQRS synchronization.
    /// </summary>
    public void DemoteToUser()
    {
        ChangeRole(UserRole.User);
    }

    /// <summary>
    /// Soft deletes the user.
    /// Raises UserDeletedEvent for CQRS synchronization.
    /// </summary>
    public void Delete()
    {
        IsDeleted = true;
        ModifiedAt = DateTime.UtcNow;

        AddDomainEvent(new UserDeletedEvent
        {
            UserId = Id
        });
    }

    private void RaiseUpdatedEvent()
    {
        AddDomainEvent(new UserUpdatedEvent
        {
            UserId = Id,
            Email = Email,
            FullName = FullName,
            Role = Role,
            IsEmailVerified = IsEmailVerified,
            IsActive = IsActive,
            LastLoginAt = LastLoginAt,
            ModifiedAt = ModifiedAt
        });
    }
}
