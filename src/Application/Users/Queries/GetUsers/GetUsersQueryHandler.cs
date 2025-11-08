using Application.Common.Interfaces;
using Application.Common.Models;
using Application.Users.Queries.GetUser;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.Users.Queries.GetUsers;

/// <summary>
/// Handler for GetUsersQuery.
/// Demonstrates: Pagination, filtering.
/// </summary>
public class GetUsersQueryHandler : IRequestHandler<GetUsersQuery, Result<PagedResult<UserDto>>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<GetUsersQueryHandler> _logger;

    public GetUsersQueryHandler(
        IUnitOfWork unitOfWork,
        ILogger<GetUsersQueryHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<PagedResult<UserDto>>> Handle(GetUsersQuery request, CancellationToken cancellationToken)
    {
        try
        {
            // Build filter
            var filter = string.IsNullOrWhiteSpace(request.SearchTerm)
                ? null
                : (System.Linq.Expressions.Expression<Func<Domain.Entities.User, bool>>)
                  (u => u.Email.Contains(request.SearchTerm) || u.FullName.Contains(request.SearchTerm));

            // Get paginated results
            var (users, totalCount) = await _unitOfWork.Users.GetPagedAsync(
                request.PageNumber,
                request.PageSize,
                filter,
                cancellationToken);

            // Map to DTOs
            var userDtos = users.Select(u => new UserDto
            {
                Id = u.Id,
                Email = u.Email,
                FullName = u.FullName,
                IsEmailVerified = u.IsEmailVerified,
                IsActive = u.IsActive,
                LastLoginAt = u.LastLoginAt,
                CreatedAt = u.CreatedAt
            });

            var result = new PagedResult<UserDto>
            {
                Items = userDtos,
                PageNumber = request.PageNumber,
                PageSize = request.PageSize,
                TotalCount = totalCount
            };

            _logger.LogDebug(
                "Retrieved {Count} users (page {Page}/{TotalPages})",
                userDtos.Count(),
                result.PageNumber,
                result.TotalPages);

            return Result<PagedResult<UserDto>>.Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving users");
            return Result<PagedResult<UserDto>>.Failure($"Failed to retrieve users: {ex.Message}");
        }
    }
}
