using Application.Common.Models;
using MediatR;

namespace Application.Addresses.Commands.DeleteAddress;

/// <summary>
/// Command to soft delete an address.
/// Writes to Command DB, syncs to Query DB via AddressDeletedEvent.
/// </summary>
public record DeleteAddressCommand : IRequest<Result<bool>>
{
    public Guid AddressId { get; init; }
}
