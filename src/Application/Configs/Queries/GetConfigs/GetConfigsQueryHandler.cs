using Application.Common.Interfaces;
using Application.Common.Models;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.Configs.Queries.GetConfigs;

public class GetConfigsQueryHandler : IRequestHandler<GetConfigsQuery, Result<IEnumerable<ConfigDto>>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<GetConfigsQueryHandler> _logger;

    public GetConfigsQueryHandler(
        IUnitOfWork unitOfWork,
        ILogger<GetConfigsQueryHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<IEnumerable<ConfigDto>>> Handle(GetConfigsQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var filter = request.OnlyActive
                ? (System.Linq.Expressions.Expression<Func<Domain.Entities.Config, bool>>)(c => c.IsActive)
                : null;

            var configs = await _unitOfWork.Configs.GetAllAsync(filter, cancellationToken);

            var configDtos = configs.Select(c => new ConfigDto
            {
                Id = c.Id,
                Key = c.Key,
                Value = c.Value,
                Description = c.Description,
                Category = c.Category,
                IsActive = c.IsActive
            });

            _logger.LogDebug("Retrieved {Count} configuration entries", configDtos.Count());

            return Result<IEnumerable<ConfigDto>>.Success(configDtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving configurations");
            return Result<IEnumerable<ConfigDto>>.Failure($"Failed to retrieve configurations: {ex.Message}");
        }
    }
}
