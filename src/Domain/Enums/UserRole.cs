namespace Domain.Enums;

/// <summary>
/// Defines the roles a user can have in the system.
/// Used for authorization and access control.
/// </summary>
public enum UserRole
{
    /// <summary>
    /// Regular user with standard permissions.
    /// </summary>
    User = 0,

    /// <summary>
    /// Administrator with full access to admin panel and all operations.
    /// </summary>
    Admin = 1
}
