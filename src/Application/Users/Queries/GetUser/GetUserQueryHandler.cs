using Application.Common.Interfaces;
using Application.Common.Models;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.Users.Queries.GetUser;

/// <summary>
/// Handler for GetUserQuery.
/// Demonstrates: Cache-aside pattern, DTO mapping.
/// </summary>
public class GetUserQueryHandler : IRequestHandler<GetUserQuery, Result<UserDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICacheService _cacheService;
    private readonly ILogger<GetUserQueryHandler> _logger;

    public GetUserQueryHandler(
        IUnitOfWork unitOfWork,
        ICacheService cacheService,
        ILogger<GetUserQueryHandler> logger)
    {
        _unitOfWork = unitOfWork;
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

            // Not in cache, get from database
            var user = await _unitOfWork.Users.GetByIdAsync(request.UserId, cancellationToken);

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
