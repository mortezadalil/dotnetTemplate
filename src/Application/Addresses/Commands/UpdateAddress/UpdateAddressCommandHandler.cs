using Application.Common.Interfaces;
using Application.Common.Models;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.Addresses.Commands.UpdateAddress;

/// <summary>
/// Handler for UpdateAddressCommand.
/// Updates address in Command DB, domain events trigger sync to Query DB.
/// </summary>
public class UpdateAddressCommandHandler : IRequestHandler<UpdateAddressCommand, Result<bool>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<UpdateAddressCommandHandler> _logger;

    public UpdateAddressCommandHandler(
        IUnitOfWork unitOfWork,
        ILogger<UpdateAddressCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<bool>> Handle(UpdateAddressCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var address = await _unitOfWork.Addresses.GetByIdAsync(request.AddressId, cancellationToken);

            if (address == null)
            {
                return Result<bool>.Failure($"Address {request.AddressId} not found");
            }

            // Update address using domain method
            address.Update(
                request.Street,
                request.Street2,
                request.City,
                request.State,
                request.PostalCode,
                request.Country,
                request.Label,
                request.IsDefault);

            // Update phone numbers if provided
            if (request.PhoneNumbers != null)
            {
                address.UpdatePhones(request.PhoneNumbers);
            }

            await _unitOfWork.Addresses.UpdateAsync(address, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            // Domain events automatically dispatched → syncs to Query DB

            _logger.LogInformation("Address updated: {AddressId}", address.Id);

            return Result<bool>.Success(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating address {AddressId}", request.AddressId);
            return Result<bool>.Failure($"Failed to update address: {ex.Message}");
        }
    }
}
