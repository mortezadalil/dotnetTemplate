using Application.Common.Models;
using MediatR;

namespace Application.Users.Commands.CreateUser;

/// <summary>
/// Command to create a new user.
/// Commands represent write operations that change system state.
/// </summary>
public record CreateUserCommand : IRequest<Result<Guid>>
{
    public string Email { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
}
