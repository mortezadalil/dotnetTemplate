using Application.Common.Interfaces;
using Application.Common.Models;
using Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.Addresses.Commands.CreateAddress;

/// <summary>
/// Handler for CreateAddressCommand.
/// Creates address in Command DB, domain events trigger sync to Query DB.
/// </summary>
public class CreateAddressCommandHandler : IRequestHandler<CreateAddressCommand, Result<Guid>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CreateAddressCommandHandler> _logger;

    public CreateAddressCommandHandler(
        IUnitOfWork unitOfWork,
        ILogger<CreateAddressCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<Guid>> Handle(CreateAddressCommand request, CancellationToken cancellationToken)
    {
        try
        {
            // Verify user exists
            var user = await _unitOfWork.Users.GetByIdAsync(request.UserId, cancellationToken);
            if (user == null)
            {
                return Result<Guid>.Failure($"User {request.UserId} not found");
            }

            // Create address using domain factory method
            var address = Address.Create(
                request.UserId,
                request.Street,
                request.City,
                request.State,
                request.PostalCode,
                request.Country,
                request.Label,
                request.Street2,
                request.IsDefault,
                request.PhoneNumbers);

            // Save to database
            await _unitOfWork.Addresses.AddAsync(address, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            // Domain events automatically dispatched → syncs to Query DB

            _logger.LogInformation("Address created for user {UserId}: {AddressId}", request.UserId, address.Id);

            return Result<Guid>.Success(address.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating address for user {UserId}", request.UserId);
            return Result<Guid>.Failure($"Failed to create address: {ex.Message}");
        }
    }
}
