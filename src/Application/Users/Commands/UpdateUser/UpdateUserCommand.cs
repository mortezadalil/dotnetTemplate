using Application.Common.Models;
using Domain.Enums;
using MediatR;

namespace Application.Users.Commands.UpdateUser;

/// <summary>
/// Admin command to update user details.
/// Writes to Command DB, syncs to Query DB via UserUpdatedEvent.
/// </summary>
public record UpdateUserCommand : IRequest<Result<bool>>
{
    public Guid UserId { get; init; }
    public string? Email { get; init; }
    public string? FullName { get; init; }
    public UserRole? Role { get; init; }
    public bool? IsActive { get; init; }
    public bool? IsEmailVerified { get; init; }
}
