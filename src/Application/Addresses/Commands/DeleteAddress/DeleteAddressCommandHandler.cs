using Application.Common.Interfaces;
using Application.Common.Models;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.Addresses.Commands.DeleteAddress;

/// <summary>
/// Handler for DeleteAddressCommand.
/// Soft deletes address in Command DB, domain events trigger sync to Query DB.
/// </summary>
public class DeleteAddressCommandHandler : IRequestHandler<DeleteAddressCommand, Result<bool>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<DeleteAddressCommandHandler> _logger;

    public DeleteAddressCommandHandler(
        IUnitOfWork unitOfWork,
        ILogger<DeleteAddressCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<bool>> Handle(DeleteAddressCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var address = await _unitOfWork.Addresses.GetByIdAsync(request.AddressId, cancellationToken);

            if (address == null)
            {
                return Result<bool>.Failure($"Address {request.AddressId} not found");
            }

            // Soft delete using domain method
            address.Delete();

            await _unitOfWork.Addresses.UpdateAsync(address, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            // Domain events automatically dispatched → syncs to Query DB

            _logger.LogInformation("Address soft deleted: {AddressId}", address.Id);

            return Result<bool>.Success(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting address {AddressId}", request.AddressId);
            return Result<bool>.Failure($"Failed to delete address: {ex.Message}");
        }
    }
}
