using Application.Common.Models;
using Application.Configs.Queries.GetConfigs;
using Application.Users.Queries.GetUsers;
using Infrastructure.Persistence.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;
using System.Linq.Expressions;

namespace Application.Configs.Queries.GetConfigsAdmin;

/// <summary>
/// Handler for GetConfigsAdminQuery.
/// Demonstrates: Admin CQRS read from Query DB with advanced filtering and pagination.
/// Uses QueryUnitOfWork to read from optimized read database.
/// </summary>
public class GetConfigsAdminQueryHandler : IRequestHandler<GetConfigsAdminQuery, Result<PagedResult<ConfigDto>>>
{
    private readonly QueryUnitOfWork _queryUnitOfWork;
    private readonly ILogger<GetConfigsAdminQueryHandler> _logger;

    public GetConfigsAdminQueryHandler(
        QueryUnitOfWork queryUnitOfWork,
        ILogger<GetConfigsAdminQueryHandler> logger)
    {
        _queryUnitOfWork = queryUnitOfWork;
        _logger = logger;
    }

    public async Task<Result<PagedResult<ConfigDto>>> Handle(GetConfigsAdminQuery request, CancellationToken cancellationToken)
    {
        try
        {
            // Build complex filter combining multiple conditions
            Expression<Func<Domain.Entities.Config, bool>>? filter = null;

            if (!string.IsNullOrWhiteSpace(request.SearchTerm) ||
                !string.IsNullOrWhiteSpace(request.Category) ||
                request.IsActive.HasValue)
            {
                filter = c =>
                    (string.IsNullOrWhiteSpace(request.SearchTerm) ||
                     c.Key.Contains(request.SearchTerm) ||
                     c.Description.Contains(request.SearchTerm)) &&
                    (string.IsNullOrWhiteSpace(request.Category) ||
                     c.Category == request.Category) &&
                    (!request.IsActive.HasValue ||
                     c.IsActive == request.IsActive.Value);
            }

            // Get paginated results from Query DB (optimized for reads)
            var (configs, totalCount) = await _queryUnitOfWork.Configs.GetPagedAsync(
                request.PageNumber,
                request.PageSize,
                filter,
                cancellationToken);

            // Map to DTOs
            var configDtos = configs.Select(c => new ConfigDto
            {
                Id = c.Id,
                Key = c.Key,
                Value = c.Value,
                Description = c.Description,
                Category = c.Category,
                IsActive = c.IsActive
            });

            var result = new PagedResult<ConfigDto>
            {
                Items = configDtos,
                PageNumber = request.PageNumber,
                PageSize = request.PageSize,
                TotalCount = totalCount
            };

            _logger.LogDebug(
                "Retrieved {Count} configs (page {Page}/{TotalPages})",
                configDtos.Count(),
                result.PageNumber,
                result.TotalPages);

            return Result<PagedResult<ConfigDto>>.Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving configs for admin");
            return Result<PagedResult<ConfigDto>>.Failure($"Failed to retrieve configs: {ex.Message}");
        }
    }
}
