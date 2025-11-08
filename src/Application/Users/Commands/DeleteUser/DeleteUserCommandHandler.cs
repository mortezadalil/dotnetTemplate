using Application.Common.Interfaces;
using Application.Common.Models;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.Users.Commands.DeleteUser;

/// <summary>
/// Handler for DeleteUserCommand.
/// Soft deletes user in Command DB, domain events trigger sync to Query DB.
/// </summary>
public class DeleteUserCommandHandler : IRequestHandler<DeleteUserCommand, Result<bool>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<DeleteUserCommandHandler> _logger;

    public DeleteUserCommandHandler(
        IUnitOfWork unitOfWork,
        ILogger<DeleteUserCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<bool>> Handle(DeleteUserCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var user = await _unitOfWork.Users.GetByIdAsync(request.UserId, cancellationToken);

            if (user == null)
            {
                return Result<bool>.Failure($"User {request.UserId} not found");
            }

            // Soft delete using domain method (raises UserDeletedEvent)
            user.Delete();

            await _unitOfWork.Users.UpdateAsync(user, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            // Domain events automatically dispatched → syncs to Query DB

            _logger.LogInformation("Admin soft-deleted user: {UserId}", user.Id);

            return Result<bool>.Success(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting user {UserId}", request.UserId);
            return Result<bool>.Failure($"Failed to delete user: {ex.Message}");
        }
    }
}
