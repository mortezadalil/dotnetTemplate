using Application.Common.Models;
using Application.Users.Commands.CreateUser;
using Application.Users.Commands.DeleteUser;
using Application.Users.Commands.UpdateUser;
using Application.Users.Queries.GetUser;
using Application.Users.Queries.GetUsers;
using Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Api.Endpoints;

/// <summary>
/// Admin endpoints for User management.
/// All endpoints require Admin role authorization.
/// </summary>
public static class AdminUsersEndpoints
{
    public static void MapAdminUsersEndpoints(this IEndpointRouteBuilder app)
    {
        var users = app.MapGroup("/api/admin/users")
            .WithTags("Admin - Users")
            .RequireAuthorization(policy => policy.RequireRole(UserRole.Admin.ToString()))
            .WithOpenApi();

        // GET /api/admin/users - List users with filtering and pagination
        users.MapGet("/", async (
            [FromServices] IMediator mediator,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? search = null,
            CancellationToken ct = default) =>
        {
            var query = new GetUsersQuery
            {
                PageNumber = page,
                PageSize = pageSize,
                SearchTerm = search
            };

            var result = await mediator.Send(query, ct);

            return result.IsSuccess
                ? Results.Ok(result.Value)
                : Results.BadRequest(result.Error);
        })
        .WithName("GetUsersAdmin")
        .WithSummary("Get paginated list of users with optional search filter")
        .Produces<PagedResult<UserDto>>(200)
        .Produces<string>(400);

        // GET /api/admin/users/{id} - Get user by ID
        users.MapGet("/{id:guid}", async (
            [FromRoute] Guid id,
            [FromServices] IMediator mediator,
            CancellationToken ct) =>
        {
            var query = new GetUserQuery { UserId = id };
            var result = await mediator.Send(query, ct);

            return result.IsSuccess
                ? Results.Ok(result.Value)
                : Results.NotFound(result.Error);
        })
        .WithName("GetUserAdmin")
        .WithSummary("Get user by ID")
        .Produces<UserDto>(200)
        .Produces<string>(404);

        // POST /api/admin/users - Create new user
        users.MapPost("/", async (
            [FromBody] CreateUserRequest request,
            [FromServices] IMediator mediator,
            CancellationToken ct) =>
        {
            var command = new CreateUserCommand
            {
                Email = request.Email,
                FullName = request.FullName,
                Password = request.Password,
                Role = request.Role ?? UserRole.User
            };

            var result = await mediator.Send(command, ct);

            return result.IsSuccess
                ? Results.Created($"/api/admin/users/{result.Value}", new { id = result.Value })
                : Results.BadRequest(result.Error);
        })
        .WithName("CreateUserAdmin")
        .WithSummary("Create a new user (Admin only)")
        .Produces<Guid>(201)
        .Produces<string>(400);

        // PUT /api/admin/users/{id} - Update user
        users.MapPut("/{id:guid}", async (
            [FromRoute] Guid id,
            [FromBody] UpdateUserRequest request,
            [FromServices] IMediator mediator,
            CancellationToken ct) =>
        {
            var command = new UpdateUserCommand
            {
                UserId = id,
                Email = request.Email,
                FullName = request.FullName,
                Role = request.Role,
                IsActive = request.IsActive,
                IsEmailVerified = request.IsEmailVerified
            };

            var result = await mediator.Send(command, ct);

            return result.IsSuccess
                ? Results.Ok(new { success = true })
                : Results.BadRequest(result.Error);
        })
        .WithName("UpdateUserAdmin")
        .WithSummary("Update user details (Admin only)")
        .Produces<object>(200)
        .Produces<string>(400);

        // DELETE /api/admin/users/{id} - Soft delete user
        users.MapDelete("/{id:guid}", async (
            [FromRoute] Guid id,
            [FromServices] IMediator mediator,
            CancellationToken ct) =>
        {
            var command = new DeleteUserCommand { UserId = id };
            var result = await mediator.Send(command, ct);

            return result.IsSuccess
                ? Results.Ok(new { success = true })
                : Results.BadRequest(result.Error);
        })
        .WithName("DeleteUserAdmin")
        .WithSummary("Soft delete user (Admin only)")
        .Produces<object>(200)
        .Produces<string>(400);
    }
}

// Request DTOs for Admin User endpoints
public record CreateUserRequest(
    string Email,
    string FullName,
    string Password,
    UserRole? Role = null);

public record UpdateUserRequest(
    string? Email = null,
    string? FullName = null,
    UserRole? Role = null,
    bool? IsActive = null,
    bool? IsEmailVerified = null);
