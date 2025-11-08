using Application.Common.Models;
using MediatR;

namespace Application.Users.Commands.DeleteUser;

/// <summary>
/// Admin command to soft delete a user.
/// Writes to Command DB, syncs to Query DB via UserDeletedEvent.
/// </summary>
public record DeleteUserCommand : IRequest<Result<bool>>
{
    public Guid UserId { get; init; }
}
