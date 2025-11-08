using Application.Common.Interfaces;
using Application.Common.Models;
using Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.Users.Commands.CreateUser;

/// <summary>
/// Handler for CreateUserCommand.
/// Demonstrates: Repository pattern, password hashing, error handling.
/// </summary>
public class CreateUserCommandHandler : IRequestHandler<CreateUserCommand, Result<Guid>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CreateUserCommandHandler> _logger;

    public CreateUserCommandHandler(
        IUnitOfWork unitOfWork,
        ILogger<CreateUserCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<Guid>> Handle(CreateUserCommand request, CancellationToken cancellationToken)
    {
        try
        {
            // Check if user already exists
            var existingUser = await _unitOfWork.Users.FindAsync(
                u => u.Email == request.Email.ToLowerInvariant(),
                cancellationToken);

            if (existingUser != null)
            {
                return Result<Guid>.Failure($"User with email {request.Email} already exists");
            }

            // Hash password (in real app, use BCrypt or similar)
            var passwordHash = HashPassword(request.Password);

            // Create user using domain factory method
            var user = User.Create(request.Email, request.FullName, passwordHash);

            // Save to database
            await _unitOfWork.Users.AddAsync(user, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("User created successfully: {Email}", user.Email);

            return Result<Guid>.Success(user.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating user: {Email}", request.Email);
            return Result<Guid>.Failure($"Failed to create user: {ex.Message}");
        }
    }

    // Note: In production, use BCrypt.Net-Next or similar
    private static string HashPassword(string password)
    {
        // Simple hash for demonstration - REPLACE IN PRODUCTION
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var bytes = System.Text.Encoding.UTF8.GetBytes(password);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }
}
