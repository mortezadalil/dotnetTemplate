using Application.Common.Models;
using Domain.Enums;
using MediatR;

namespace Application.Users.Queries.GetUser;

/// <summary>
/// Query to get a user by ID.
/// Queries represent read operations that don't change system state.
/// </summary>
public record GetUserQuery : IRequest<Result<UserDto>>
{
    public Guid UserId { get; init; }
}

/// <summary>
/// Data Transfer Object for User.
/// Never expose domain entities directly to the API.
/// </summary>
public record UserDto
{
    public Guid Id { get; init; }
    public string Email { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public UserRole Role { get; init; }
    public bool IsEmailVerified { get; init; }
    public bool IsActive { get; init; }
    public DateTime? LastLoginAt { get; init; }
    public DateTime CreatedAt { get; init; }
}
