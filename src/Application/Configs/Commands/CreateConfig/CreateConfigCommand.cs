using Application.Common.Models;
using MediatR;

namespace Application.Configs.Commands.CreateConfig;

/// <summary>
/// Admin command to create a new configuration entry.
/// Writes to Command DB, syncs to Query DB via ConfigCreatedEvent.
/// </summary>
public record CreateConfigCommand : IRequest<Result<Guid>>
{
    public string Key { get; init; } = string.Empty;
    public string Value { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Category { get; init; } = "General";
    public bool IsActive { get; init; } = true;
}
