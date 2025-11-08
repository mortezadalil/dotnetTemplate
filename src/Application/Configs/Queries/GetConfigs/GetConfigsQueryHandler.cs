using Application.Common.Interfaces;
using Application.Common.Models;
using Infrastructure.Persistence.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.Configs.Queries.GetConfigs;

/// <summary>
/// Handler for GetConfigsQuery.
/// Uses QueryUnitOfWork to read from optimized read database.
/// </summary>
public class GetConfigsQueryHandler : IRequestHandler<GetConfigsQuery, Result<IEnumerable<ConfigDto>>>
{
    private readonly QueryUnitOfWork _queryUnitOfWork;
    private readonly ILogger<GetConfigsQueryHandler> _logger;

    public GetConfigsQueryHandler(
        QueryUnitOfWork queryUnitOfWork,
        ILogger<GetConfigsQueryHandler> logger)
    {
        _queryUnitOfWork = queryUnitOfWork;
        _logger = logger;
    }

    public async Task<Result<IEnumerable<ConfigDto>>> Handle(GetConfigsQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var filter = request.OnlyActive
                ? (System.Linq.Expressions.Expression<Func<Domain.Entities.Config, bool>>)(c => c.IsActive)
                : null;

            var configs = await _queryUnitOfWork.Configs.GetAllAsync(filter, cancellationToken);

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
