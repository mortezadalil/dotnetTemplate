using Application.Common.Models;
using MediatR;

namespace Application.Addresses.Queries.GetUserAddresses;

/// <summary>
/// Query to get all addresses for a user.
/// Reads from Query DB for optimal performance.
/// </summary>
public record GetUserAddressesQuery : IRequest<Result<List<AddressDto>>>
{
    public Guid UserId { get; init; }
}

/// <summary>
/// DTO for Address with phone numbers.
/// </summary>
public record AddressDto
{
    public Guid Id { get; init; }
    public Guid UserId { get; init; }
    public string Street { get; init; } = string.Empty;
    public string? Street2 { get; init; }
    public string City { get; init; } = string.Empty;
    public string State { get; init; } = string.Empty;
    public string PostalCode { get; init; } = string.Empty;
    public string Country { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public bool IsDefault { get; init; }
    public List<string> PhoneNumbers { get; init; } = new();
    public DateTime CreatedAt { get; init; }
}
