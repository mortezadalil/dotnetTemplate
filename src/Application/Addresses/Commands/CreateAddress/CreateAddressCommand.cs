using Application.Common.Models;
using MediatR;

namespace Application.Addresses.Commands.CreateAddress;

/// <summary>
/// Command to create a new address for a user.
/// Writes to Command DB, syncs to Query DB via AddressCreatedEvent.
/// </summary>
public record CreateAddressCommand : IRequest<Result<Guid>>
{
    public Guid UserId { get; init; }
    public string Street { get; init; } = string.Empty;
    public string? Street2 { get; init; }
    public string City { get; init; } = string.Empty;
    public string State { get; init; } = string.Empty;
    public string PostalCode { get; init; } = string.Empty;
    public string Country { get; init; } = string.Empty;
    public string Label { get; init; } = "Home";
    public bool IsDefault { get; init; }
    public List<string> PhoneNumbers { get; init; } = new();
}
