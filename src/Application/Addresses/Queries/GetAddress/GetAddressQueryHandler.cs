using Application.Addresses.Queries.GetUserAddresses;
using Application.Common.Models;
using Infrastructure.Persistence.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.Addresses.Queries.GetAddress;

/// <summary>
/// Handler for GetAddressQuery.
/// Reads from Query DB for optimal performance.
/// </summary>
public class GetAddressQueryHandler : IRequestHandler<GetAddressQuery, Result<AddressDto>>
{
    private readonly QueryUnitOfWork _queryUnitOfWork;
    private readonly ILogger<GetAddressQueryHandler> _logger;

    public GetAddressQueryHandler(
        QueryUnitOfWork queryUnitOfWork,
        ILogger<GetAddressQueryHandler> logger)
    {
        _queryUnitOfWork = queryUnitOfWork;
        _logger = logger;
    }

    public async Task<Result<AddressDto>> Handle(GetAddressQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var address = await _queryUnitOfWork.Addresses.GetByIdAsync(request.AddressId, cancellationToken);

            if (address == null)
            {
                _logger.LogWarning("Address {AddressId} not found", request.AddressId);
                return Result<AddressDto>.Failure("Address not found");
            }

            var addressDto = new AddressDto
            {
                Id = address.Id,
                UserId = address.UserId,
                Street = address.Street,
                Street2 = address.Street2,
                City = address.City,
                State = address.State,
                PostalCode = address.PostalCode,
                Country = address.Country,
                Label = address.Label,
                IsDefault = address.IsDefault,
                PhoneNumbers = address.Phones.Select(p => p.Number).ToList(),
                CreatedAt = address.CreatedAt
            };

            _logger.LogDebug("Retrieved address {AddressId}", request.AddressId);

            return Result<AddressDto>.Success(addressDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving address {AddressId}", request.AddressId);
            return Result<AddressDto>.Failure($"Failed to retrieve address: {ex.Message}");
        }
    }
}
