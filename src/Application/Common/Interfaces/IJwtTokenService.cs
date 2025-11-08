using Domain.Entities;

namespace Application.Common.Interfaces;

/// <summary>
/// Service for generating and validating JWT tokens.
/// </summary>
public interface IJwtTokenService
{
    /// <summary>
    /// Generates a JWT token for the given user.
    /// </summary>
    string GenerateToken(User user);

    /// <summary>
    /// Validates a JWT token and returns the user ID if valid.
    /// </summary>
    Task<Guid?> ValidateTokenAsync(string token);
}
