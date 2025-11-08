using Application.Common.Interfaces;
using Application.Common.Models;
using Domain.Entities;
using Domain.Events;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.Configs.Commands.CreateConfig;

/// <summary>
/// Handler for CreateConfigCommand.
/// Creates config in Command DB, domain events trigger sync to Query DB.
/// </summary>
public class CreateConfigCommandHandler : IRequestHandler<CreateConfigCommand, Result<Guid>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CreateConfigCommandHandler> _logger;

    public CreateConfigCommandHandler(
        IUnitOfWork unitOfWork,
        ILogger<CreateConfigCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<Guid>> Handle(CreateConfigCommand request, CancellationToken cancellationToken)
    {
        try
        {
            // Check if config key already exists
            var existingConfig = await _unitOfWork.Configs.FindAsync(
                c => c.Key == request.Key,
                cancellationToken);

            if (existingConfig != null)
            {
                return Result<Guid>.Failure($"Configuration with key '{request.Key}' already exists");
            }

            // Create config using domain factory method
            var config = Config.Create(request.Key, request.Value, request.Description, request.Category);

            if (!request.IsActive)
            {
                config.Deactivate();
            }

            // Raise domain event for synchronization (we need to add this to Config entity)
            // For now, we'll handle it manually in the event handler section

            await _unitOfWork.Configs.AddAsync(config, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            // Domain events automatically dispatched → syncs to Query DB

            _logger.LogInformation("Admin created config: {Key}", config.Key);

            return Result<Guid>.Success(config.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating config: {Key}", request.Key);
            return Result<Guid>.Failure($"Failed to create config: {ex.Message}");
        }
    }
}
