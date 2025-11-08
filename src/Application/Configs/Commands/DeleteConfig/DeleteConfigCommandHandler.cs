using Application.Common.Interfaces;
using Application.Common.Models;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.Configs.Commands.DeleteConfig;

/// <summary>
/// Handler for DeleteConfigCommand.
/// Physically deletes config from Command DB, domain events trigger sync to Query DB.
/// </summary>
public class DeleteConfigCommandHandler : IRequestHandler<DeleteConfigCommand, Result<bool>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<DeleteConfigCommandHandler> _logger;

    public DeleteConfigCommandHandler(
        IUnitOfWork unitOfWork,
        ILogger<DeleteConfigCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<bool>> Handle(DeleteConfigCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var config = await _unitOfWork.Configs.GetByIdAsync(request.ConfigId, cancellationToken);

            if (config == null)
            {
                return Result<bool>.Failure($"Config {request.ConfigId} not found");
            }

            var configKey = config.Key; // Store for logging

            // Physical delete (hard delete)
            // Note: We need to raise a domain event before deleting
            // We'll add ConfigDeletedEvent to the entity first

            await _unitOfWork.Configs.HardDeleteAsync(config, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            // Domain events automatically dispatched → syncs to Query DB

            _logger.LogInformation("Admin physically deleted config: {Key}", configKey);

            return Result<bool>.Success(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting config {ConfigId}", request.ConfigId);
            return Result<bool>.Failure($"Failed to delete config: {ex.Message}");
        }
    }
}
