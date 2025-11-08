using Application.Common.Models;
using Application.Configs.Queries.GetConfigs;
using Application.Users.Queries.GetUsers;
using MediatR;

namespace Application.Configs.Queries.GetConfigsAdmin;

/// <summary>
/// Admin query to get a paginated list of configurations with filtering.
/// Supports pagination, search by key/category, and active status filtering.
/// </summary>
public record GetConfigsAdminQuery : IRequest<Result<PagedResult<ConfigDto>>>
{
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 10;
    public string? SearchTerm { get; init; }
    public string? Category { get; init; }
    public bool? IsActive { get; init; }
}
