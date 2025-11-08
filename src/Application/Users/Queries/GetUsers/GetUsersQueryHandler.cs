using Application.Addresses.Queries.GetUserAddresses;
using Application.Common.Interfaces;
using Application.Common.Models;
using Application.Users.Queries.GetUser;
using Infrastructure.Persistence.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.Users.Queries.GetUsers;

/// <summary>
/// Handler for GetUsersQuery.
/// Demonstrates: CQRS read from Query DB, Pagination, filtering.
/// Uses QueryUnitOfWork to read from optimized read database.
/// </summary>
public class GetUsersQueryHandler : IRequestHandler<GetUsersQuery, Result<PagedResult<UserDto>>>
{
    private readonly QueryUnitOfWork _queryUnitOfWork;
    private readonly ILogger<GetUsersQueryHandler> _logger;

    public GetUsersQueryHandler(
        QueryUnitOfWork queryUnitOfWork,
        ILogger<GetUsersQueryHandler> logger)
    {
        _queryUnitOfWork = queryUnitOfWork;
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

            // Get paginated results from Query DB (optimized for reads)
            var (users, totalCount) = await _queryUnitOfWork.Users.GetPagedAsync(
                request.PageNumber,
                request.PageSize,
                filter,
                cancellationToken);

            // Map to DTOs
            var userDtos = new List<UserDto>();
            foreach (var u in users)
            {
                List<AddressDto>? addresses = null;

                // Include addresses if requested
                if (request.IncludeAddresses)
                {
                    var userAddresses = await _queryUnitOfWork.Addresses
                        .FindAllAsync(a => a.UserId == u.Id, cancellationToken);

                    addresses = userAddresses.Select(a => new AddressDto
                    {
                        Id = a.Id,
                        UserId = a.UserId,
                        Street = a.Street,
                        Street2 = a.Street2,
                        City = a.City,
                        State = a.State,
                        PostalCode = a.PostalCode,
                        Country = a.Country,
                        Label = a.Label,
                        IsDefault = a.IsDefault,
                        PhoneNumbers = a.Phones.Select(p => p.Number).ToList(),
                        CreatedAt = a.CreatedAt
                    }).ToList();
                }

                userDtos.Add(new UserDto
                {
                    Id = u.Id,
                    Email = u.Email,
                    FullName = u.FullName,
                    Role = u.Role,
                    IsEmailVerified = u.IsEmailVerified,
                    IsActive = u.IsActive,
                    LastLoginAt = u.LastLoginAt,
                    CreatedAt = u.CreatedAt,
                    Addresses = addresses
                });
            }

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
