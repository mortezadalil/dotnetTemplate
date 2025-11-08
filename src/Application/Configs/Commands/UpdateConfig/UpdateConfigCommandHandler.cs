using Application.Common.Interfaces;
using Application.Common.Models;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.Configs.Commands.UpdateConfig;

/// <summary>
/// Handler for UpdateConfigCommand.
/// Updates config in Command DB, domain events trigger sync to Query DB.
/// </summary>
public class UpdateConfigCommandHandler : IRequestHandler<UpdateConfigCommand, Result<bool>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<UpdateConfigCommandHandler> _logger;

    public UpdateConfigCommandHandler(
        IUnitOfWork unitOfWork,
        ILogger<UpdateConfigCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<bool>> Handle(UpdateConfigCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var config = await _unitOfWork.Configs.GetByIdAsync(request.ConfigId, cancellationToken);

            if (config == null)
            {
                return Result<bool>.Failure($"Config {request.ConfigId} not found");
            }

            // Update using domain methods
            if (request.Value != null)
            {
                config.UpdateValue(request.Value);
            }

            if (request.Description != null)
            {
                config.UpdateDescription(request.Description);
            }

            if (request.Category != null)
            {
                // Update category via reflection (Config has private setter)
                typeof(Domain.Entities.Config)
                    .GetProperty("Category")!
                    .SetValue(config, request.Category);
                config.ModifiedAt = DateTime.UtcNow;
            }

            if (request.IsActive.HasValue)
            {
                if (request.IsActive.Value)
                    config.Activate();
                else
                    config.Deactivate();
            }

            await _unitOfWork.Configs.UpdateAsync(config, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            // Domain events automatically dispatched → syncs to Query DB

            _logger.LogInformation("Admin updated config: {Key}", config.Key);

            return Result<bool>.Success(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating config {ConfigId}", request.ConfigId);
            return Result<bool>.Failure($"Failed to update config: {ex.Message}");
        }
    }
}
