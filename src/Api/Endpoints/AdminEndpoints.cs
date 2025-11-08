using Application.Common.Models;
using Application.Configs.Commands.CreateConfig;
using Application.Configs.Commands.DeleteConfig;
using Application.Configs.Commands.UpdateConfig;
using Application.Configs.Queries.GetConfigs;
using Application.Configs.Queries.GetConfigsAdmin;
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
/// Admin panel endpoints for managing users and configurations.
/// All endpoints require Admin role authorization.
/// </summary>
public static class AdminEndpoints
{
    public static void MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var admin = app.MapGroup("/api/admin")
            .WithTags("Admin Panel")
            .RequireAuthorization(policy => policy.RequireRole(UserRole.Admin.ToString()));

        MapUserAdminEndpoints(admin);
        MapConfigAdminEndpoints(admin);
    }

    /// <summary>
    /// Admin endpoints for User management.
    /// </summary>
    private static void MapUserAdminEndpoints(RouteGroupBuilder group)
    {
        var users = group.MapGroup("/users").WithOpenApi();

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

    /// <summary>
    /// Admin endpoints for Config management.
    /// </summary>
    private static void MapConfigAdminEndpoints(RouteGroupBuilder group)
    {
        var configs = group.MapGroup("/configs").WithOpenApi();

        // GET /api/admin/configs - List configs with filtering and pagination
        configs.MapGet("/", async (
            [FromServices] IMediator mediator,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? search = null,
            [FromQuery] string? category = null,
            [FromQuery] bool? isActive = null,
            CancellationToken ct = default) =>
        {
            var query = new GetConfigsAdminQuery
            {
                PageNumber = page,
                PageSize = pageSize,
                SearchTerm = search,
                Category = category,
                IsActive = isActive
            };

            var result = await mediator.Send(query, ct);

            return result.IsSuccess
                ? Results.Ok(result.Value)
                : Results.BadRequest(result.Error);
        })
        .WithName("GetConfigsAdmin")
        .WithSummary("Get paginated list of configs with optional filters")
        .Produces<PagedResult<ConfigDto>>(200)
        .Produces<string>(400);

        // POST /api/admin/configs - Create new config
        configs.MapPost("/", async (
            [FromBody] CreateConfigRequest request,
            [FromServices] IMediator mediator,
            CancellationToken ct) =>
        {
            var command = new CreateConfigCommand
            {
                Key = request.Key,
                Value = request.Value,
                Description = request.Description ?? string.Empty,
                Category = request.Category ?? "General",
                IsActive = request.IsActive ?? true
            };

            var result = await mediator.Send(command, ct);

            return result.IsSuccess
                ? Results.Created($"/api/admin/configs/{result.Value}", new { id = result.Value })
                : Results.BadRequest(result.Error);
        })
        .WithName("CreateConfigAdmin")
        .WithSummary("Create a new configuration (Admin only)")
        .Produces<Guid>(201)
        .Produces<string>(400);

        // PUT /api/admin/configs/{id} - Update config
        configs.MapPut("/{id:guid}", async (
            [FromRoute] Guid id,
            [FromBody] UpdateConfigRequest request,
            [FromServices] IMediator mediator,
            CancellationToken ct) =>
        {
            var command = new UpdateConfigCommand
            {
                ConfigId = id,
                Value = request.Value,
                Description = request.Description,
                Category = request.Category,
                IsActive = request.IsActive
            };

            var result = await mediator.Send(command, ct);

            return result.IsSuccess
                ? Results.Ok(new { success = true })
                : Results.BadRequest(result.Error);
        })
        .WithName("UpdateConfigAdmin")
        .WithSummary("Update configuration (Admin only)")
        .Produces<object>(200)
        .Produces<string>(400);

        // DELETE /api/admin/configs/{id} - Physical delete config
        configs.MapDelete("/{id:guid}", async (
            [FromRoute] Guid id,
            [FromServices] IMediator mediator,
            CancellationToken ct) =>
        {
            var command = new DeleteConfigCommand { ConfigId = id };
            var result = await mediator.Send(command, ct);

            return result.IsSuccess
                ? Results.Ok(new { success = true })
                : Results.BadRequest(result.Error);
        })
        .WithName("DeleteConfigAdmin")
        .WithSummary("Physical delete configuration (Admin only)")
        .Produces<object>(200)
        .Produces<string>(400);
    }
}

// Request DTOs for Admin endpoints
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

public record CreateConfigRequest(
    string Key,
    string Value,
    string? Description = null,
    string? Category = null,
    bool? IsActive = null);

public record UpdateConfigRequest(
    string? Value = null,
    string? Description = null,
    string? Category = null,
    bool? IsActive = null);
