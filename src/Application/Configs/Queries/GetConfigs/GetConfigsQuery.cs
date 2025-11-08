using Application.Common.Models;
using MediatR;

namespace Application.Configs.Queries.GetConfigs;

/// <summary>
/// Query to get all active configuration entries.
/// Used by AppConfig to refresh configuration.
/// </summary>
public record GetConfigsQuery : IRequest<Result<IEnumerable<ConfigDto>>>
{
    public bool OnlyActive { get; init; } = true;
}

public record ConfigDto
{
    public Guid Id { get; init; }
    public string Key { get; init; } = string.Empty;
    public string Value { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public bool IsActive { get; init; }
}
