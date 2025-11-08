using Application.Common.Models;
using Infrastructure.Persistence.Repositories;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Application.Addresses.Queries.GetUserAddresses;

/// <summary>
/// Handler for GetUserAddressesQuery.
/// Reads from Query DB for optimal performance.
/// </summary>
public class GetUserAddressesQueryHandler : IRequestHandler<GetUserAddressesQuery, Result<List<AddressDto>>>
{
    private readonly QueryUnitOfWork _queryUnitOfWork;
    private readonly ILogger<GetUserAddressesQueryHandler> _logger;

    public GetUserAddressesQueryHandler(
        QueryUnitOfWork queryUnitOfWork,
        ILogger<GetUserAddressesQueryHandler> logger)
    {
        _queryUnitOfWork = queryUnitOfWork;
        _logger = logger;
    }

    public async Task<Result<List<AddressDto>>> Handle(GetUserAddressesQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var addresses = await _queryUnitOfWork.Addresses
                .FindAllAsync(a => a.UserId == request.UserId, cancellationToken);

            var addressDtos = addresses.Select(a => new AddressDto
            {
                Id = a.Id,
                UserId = a.UserId,
                Street = a.Street,
                Street2 = a.Street2,
                City = a.City,
                State = a.State,
                PostalCode = a.PostalCode,
                Country = a.Country,
                Label = a.Label,
                IsDefault = a.IsDefault,
                PhoneNumbers = a.Phones.Select(p => p.Number).ToList(),
                CreatedAt = a.CreatedAt
            }).ToList();

            _logger.LogDebug("Retrieved {Count} addresses for user {UserId}", addressDtos.Count, request.UserId);

            return Result<List<AddressDto>>.Success(addressDtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving addresses for user {UserId}", request.UserId);
            return Result<List<AddressDto>>.Failure($"Failed to retrieve addresses: {ex.Message}");
        }
    }
}
