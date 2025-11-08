using Application.Addresses.Queries.GetUserAddresses;
using Application.Common.Models;
using MediatR;

namespace Application.Addresses.Queries.GetAddress;

/// <summary>
/// Query to get a single address by ID.
/// Reads from Query DB for optimal performance.
/// </summary>
public record GetAddressQuery : IRequest<Result<AddressDto>>
{
    public Guid AddressId { get; init; }
}
