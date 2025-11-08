using Application.Common.Models;
using MediatR;

namespace Application.Configs.Commands.UpdateConfig;

/// <summary>
/// Admin command to update an existing configuration entry.
/// Writes to Command DB, syncs to Query DB via ConfigUpdatedEvent.
/// </summary>
public record UpdateConfigCommand : IRequest<Result<bool>>
{
    public Guid ConfigId { get; init; }
    public string? Value { get; init; }
    public string? Description { get; init; }
    public string? Category { get; init; }
    public bool? IsActive { get; init; }
}
