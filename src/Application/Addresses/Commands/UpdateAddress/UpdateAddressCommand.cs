using Application.Common.Models;
using MediatR;

namespace Application.Addresses.Commands.UpdateAddress;

/// <summary>
/// Command to update an existing address.
/// Writes to Command DB, syncs to Query DB via AddressUpdatedEvent.
/// </summary>
public record UpdateAddressCommand : IRequest<Result<bool>>
{
    public Guid AddressId { get; init; }
    public string? Street { get; init; }
    public string? Street2 { get; init; }
    public string? City { get; init; }
    public string? State { get; init; }
    public string? PostalCode { get; init; }
    public string? Country { get; init; }
    public string? Label { get; init; }
    public bool? IsDefault { get; init; }
    public List<string>? PhoneNumbers { get; init; }
}
