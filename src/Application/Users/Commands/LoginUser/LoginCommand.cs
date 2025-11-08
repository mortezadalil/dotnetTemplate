using Application.Common.Models;
using MediatR;

namespace Application.Users.Commands.LoginUser;

/// <summary>
/// Command to authenticate a user and return a JWT token.
/// </summary>
public record LoginCommand : IRequest<Result<LoginResponse>>
{
    public string Email { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
}

/// <summary>
/// Response for successful login.
/// </summary>
public record LoginResponse
{
    public string Token { get; init; } = string.Empty;
    public Guid UserId { get; init; }
    public string Email { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
}
