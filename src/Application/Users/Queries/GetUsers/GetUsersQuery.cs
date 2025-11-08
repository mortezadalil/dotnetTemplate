using Application.Common.Models;
using Application.Users.Queries.GetUser;
using MediatR;

namespace Application.Users.Queries.GetUsers;

/// <summary>
/// Query to get a paginated list of users.
/// Demonstrates pagination pattern.
/// </summary>
public record GetUsersQuery : IRequest<Result<PagedResult<UserDto>>>
{
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 10;
    public string? SearchTerm { get; init; }
    public bool IncludeAddresses { get; init; } = false;
}

/// <summary>
/// Paginated result wrapper.
/// </summary>
public record PagedResult<T>
{
    public IEnumerable<T> Items { get; init; } = new List<T>();
    public int PageNumber { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasPreviousPage => PageNumber > 1;
    public bool HasNextPage => PageNumber < TotalPages;
}
