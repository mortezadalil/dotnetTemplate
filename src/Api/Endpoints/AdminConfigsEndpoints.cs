using Application.Common.Models;
using Application.Configs.Commands.CreateConfig;
using Application.Configs.Commands.DeleteConfig;
using Application.Configs.Commands.UpdateConfig;
using Application.Configs.Queries.GetConfigs;
using Application.Configs.Queries.GetConfigsAdmin;
using Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Api.Endpoints;

/// <summary>
/// Admin endpoints for Config management.
/// All endpoints require Admin role authorization.
/// </summary>
public static class AdminConfigsEndpoints
{
    public static void MapAdminConfigsEndpoints(this IEndpointRouteBuilder app)
    {
        var configs = app.MapGroup("/api/admin/configs")
            .WithTags("Admin - Configs")
            .RequireAuthorization(policy => policy.RequireRole(UserRole.Admin.ToString()))
            .WithOpenApi();

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

// Request DTOs for Admin Config endpoints
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
