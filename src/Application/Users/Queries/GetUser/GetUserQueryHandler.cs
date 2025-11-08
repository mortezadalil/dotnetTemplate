using Application.Common.Interfaces;
using Application.Common.Models;
using Infrastructure.Persistence.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.Users.Queries.GetUser;

/// <summary>
/// Handler for GetUserQuery.
/// Demonstrates: CQRS read from Query DB, Cache-aside pattern, DTO mapping.
/// Uses QueryUnitOfWork to read from optimized read database.
/// </summary>
public class GetUserQueryHandler : IRequestHandler<GetUserQuery, Result<UserDto>>
{
    private readonly QueryUnitOfWork _queryUnitOfWork;
    private readonly ICacheService _cacheService;
    private readonly ILogger<GetUserQueryHandler> _logger;

    public GetUserQueryHandler(
        QueryUnitOfWork queryUnitOfWork,
        ICacheService cacheService,
        ILogger<GetUserQueryHandler> logger)
    {
        _queryUnitOfWork = queryUnitOfWork;
        _cacheService = cacheService;
        _logger = logger;
    }

    public async Task<Result<UserDto>> Handle(GetUserQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var cacheKey = $"user:{request.UserId}";

            // Try cache first (cache-aside pattern)
            var cachedUser = await _cacheService.GetAsync<UserDto>(cacheKey, cancellationToken);
            if (cachedUser != null)
            {
                _logger.LogDebug("User {UserId} found in cache", request.UserId);
                return Result<UserDto>.Success(cachedUser);
            }

            // Not in cache, get from Query DB (optimized for reads)
            var user = await _queryUnitOfWork.Users.GetByIdAsync(request.UserId, cancellationToken);

            if (user == null)
            {
                _logger.LogWarning("User {UserId} not found", request.UserId);
                return Result<UserDto>.Failure("User not found");
            }

            // Map to DTO
            var userDto = new UserDto
            {
                Id = user.Id,
                Email = user.Email,
                FullName = user.FullName,
                Role = user.Role,
                IsEmailVerified = user.IsEmailVerified,
                IsActive = user.IsActive,
                LastLoginAt = user.LastLoginAt,
                CreatedAt = user.CreatedAt
            };

            // Cache for 5 minutes
            await _cacheService.SetAsync(cacheKey, userDto, TimeSpan.FromMinutes(5), cancellationToken);

            _logger.LogDebug("User {UserId} retrieved from database and cached", request.UserId);

            return Result<UserDto>.Success(userDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving user {UserId}", request.UserId);
            return Result<UserDto>.Failure($"Failed to retrieve user: {ex.Message}");
        }
    }
}
