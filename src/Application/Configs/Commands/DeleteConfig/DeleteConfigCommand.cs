using Application.Common.Models;
using MediatR;

namespace Application.Configs.Commands.DeleteConfig;

/// <summary>
/// Admin command to physically delete a configuration entry.
/// Writes to Command DB, syncs to Query DB via ConfigDeletedEvent.
/// </summary>
public record DeleteConfigCommand : IRequest<Result<bool>>
{
    public Guid ConfigId { get; init; }
}
