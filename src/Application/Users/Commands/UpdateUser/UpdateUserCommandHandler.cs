using Application.Common.Interfaces;
using Application.Common.Models;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.Users.Commands.UpdateUser;

/// <summary>
/// Handler for UpdateUserCommand.
/// Updates user in Command DB, domain events trigger sync to Query DB.
/// </summary>
public class UpdateUserCommandHandler : IRequestHandler<UpdateUserCommand, Result<bool>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<UpdateUserCommandHandler> _logger;

    public UpdateUserCommandHandler(
        IUnitOfWork unitOfWork,
        ILogger<UpdateUserCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<bool>> Handle(UpdateUserCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var user = await _unitOfWork.Users.GetByIdAsync(request.UserId, cancellationToken);

            if (user == null)
            {
                return Result<bool>.Failure($"User {request.UserId} not found");
            }

            // Update properties if provided
            // Note: In production, you'd use reflection or a mapper, or add Update methods to entity
            // For now, we'll use the existing domain methods and reflection for other properties

            if (request.Email != null)
            {
                // Update email via reflection (User has private setter)
                typeof(Domain.Entities.User)
                    .GetProperty("Email")!
                    .SetValue(user, request.Email.ToLowerInvariant());
            }

            if (request.FullName != null)
            {
                typeof(Domain.Entities.User)
                    .GetProperty("FullName")!
                    .SetValue(user, request.FullName);
            }

            if (request.IsEmailVerified.HasValue && request.IsEmailVerified.Value)
            {
                user.VerifyEmail(); // Uses domain method
            }

            if (request.IsActive.HasValue)
            {
                if (request.IsActive.Value)
                    user.Activate();
                else
                    user.Deactivate();
            }

            if (request.Role.HasValue)
            {
                user.ChangeRole(request.Role.Value);
            }

            await _unitOfWork.Users.UpdateAsync(user, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            // Domain events automatically dispatched → syncs to Query DB

            _logger.LogInformation("Admin updated user: {UserId}", user.Id);

            return Result<bool>.Success(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating user {UserId}", request.UserId);
            return Result<bool>.Failure($"Failed to update user: {ex.Message}");
        }
    }
}
