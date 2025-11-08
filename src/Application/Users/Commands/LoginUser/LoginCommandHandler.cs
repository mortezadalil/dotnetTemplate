using Application.Common.Interfaces;
using Application.Common.Models;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.Users.Commands.LoginUser;

/// <summary>
/// Handler for LoginCommand.
/// Demonstrates: Authentication, JWT generation, domain method usage.
/// </summary>
public class LoginCommandHandler : IRequestHandler<LoginCommand, Result<LoginResponse>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly ILogger<LoginCommandHandler> _logger;

    public LoginCommandHandler(
        IUnitOfWork unitOfWork,
        IJwtTokenService jwtTokenService,
        ILogger<LoginCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _jwtTokenService = jwtTokenService;
        _logger = logger;
    }

    public async Task<Result<LoginResponse>> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        try
        {
            // Find user by email
            var user = await _unitOfWork.Users.FindAsync(
                u => u.Email == request.Email.ToLowerInvariant(),
                cancellationToken);

            if (user == null)
            {
                _logger.LogWarning("Login attempt for non-existent user: {Email}", request.Email);
                return Result<LoginResponse>.Failure("Invalid email or password");
            }

            // Verify password
            var passwordHash = HashPassword(request.Password);
            if (user.PasswordHash != passwordHash)
            {
                _logger.LogWarning("Failed login attempt for user: {Email}", request.Email);
                return Result<LoginResponse>.Failure("Invalid email or password");
            }

            // Check if user is active
            if (!user.IsActive)
            {
                _logger.LogWarning("Login attempt for inactive user: {Email}", request.Email);
                return Result<LoginResponse>.Failure("Account is inactive");
            }

            // Record login in domain
            user.RecordLogin();
            await _unitOfWork.Users.UpdateAsync(user, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            // Generate JWT token
            var token = _jwtTokenService.GenerateToken(user);

            _logger.LogInformation("User logged in successfully: {Email}", user.Email);

            var response = new LoginResponse
            {
                Token = token,
                UserId = user.Id,
                Email = user.Email,
                FullName = user.FullName
            };

            return Result<LoginResponse>.Success(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during login: {Email}", request.Email);
            return Result<LoginResponse>.Failure($"Login failed: {ex.Message}");
        }
    }

    // Note: Should match the hashing in CreateUserCommandHandler
    private static string HashPassword(string password)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var bytes = System.Text.Encoding.UTF8.GetBytes(password);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }
}
