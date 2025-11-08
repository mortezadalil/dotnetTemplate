using Application.Addresses.Commands.CreateAddress;
using Application.Addresses.Commands.DeleteAddress;
using Application.Addresses.Commands.UpdateAddress;
using Application.Addresses.Queries.GetAddress;
using Application.Addresses.Queries.GetUserAddresses;
using Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Api.Endpoints;

/// <summary>
/// Endpoints for address management.
/// Users can manage their own addresses, Admins can manage any user's addresses.
/// </summary>
public static class AddressEndpoints
{
    public static void MapAddressEndpoints(this IEndpointRouteBuilder app)
    {
        var addresses = app.MapGroup("/api/addresses")
            .WithTags("Addresses")
            .RequireAuthorization() // All endpoints require authentication
            .WithOpenApi();

        // GET /api/addresses/user/{userId} - Get all addresses for a user
        addresses.MapGet("/user/{userId:guid}", async (
            [FromRoute] Guid userId,
            [FromServices] IMediator mediator,
            ClaimsPrincipal user,
            CancellationToken ct) =>
        {
            // Check authorization: user can only see own addresses, unless admin
            if (!await IsAuthorizedForUser(userId, user))
            {
                return Results.Forbid();
            }

            var query = new GetUserAddressesQuery { UserId = userId };
            var result = await mediator.Send(query, ct);

            return result.IsSuccess
                ? Results.Ok(result.Value)
                : Results.BadRequest(result.Error);
        })
        .WithName("GetUserAddresses")
        .WithSummary("Get all addresses for a user (User: own only, Admin: any user)")
        .Produces<List<AddressDto>>(200)
        .Produces(403)
        .Produces<string>(400);

        // GET /api/addresses/{id} - Get single address by ID
        addresses.MapGet("/{id:guid}", async (
            [FromRoute] Guid id,
            [FromServices] IMediator mediator,
            ClaimsPrincipal user,
            CancellationToken ct) =>
        {
            var query = new GetAddressQuery { AddressId = id };
            var result = await mediator.Send(query, ct);

            if (!result.IsSuccess)
                return Results.NotFound(result.Error);

            // Check authorization: user can only see own addresses, unless admin
            if (!await IsAuthorizedForUser(result.Value!.UserId, user))
            {
                return Results.Forbid();
            }

            return Results.Ok(result.Value);
        })
        .WithName("GetAddress")
        .WithSummary("Get address by ID (User: own only, Admin: any address)")
        .Produces<AddressDto>(200)
        .Produces(403)
        .Produces(404);

        // POST /api/addresses - Create new address
        addresses.MapPost("/", async (
            [FromBody] CreateAddressRequest request,
            [FromServices] IMediator mediator,
            ClaimsPrincipal user,
            CancellationToken ct) =>
        {
            // Check authorization: user can only create for self, unless admin
            if (!await IsAuthorizedForUser(request.UserId, user))
            {
                return Results.Forbid();
            }

            var command = new CreateAddressCommand
            {
                UserId = request.UserId,
                Street = request.Street,
                Street2 = request.Street2,
                City = request.City,
                State = request.State,
                PostalCode = request.PostalCode,
                Country = request.Country,
                Label = request.Label ?? "Home",
                IsDefault = request.IsDefault ?? false,
                PhoneNumbers = request.PhoneNumbers ?? new List<string>()
            };

            var result = await mediator.Send(command, ct);

            return result.IsSuccess
                ? Results.Created($"/api/addresses/{result.Value}", new { id = result.Value })
                : Results.BadRequest(result.Error);
        })
        .WithName("CreateAddress")
        .WithSummary("Create new address (User: own only, Admin: any user)")
        .Produces<Guid>(201)
        .Produces(403)
        .Produces<string>(400);

        // PUT /api/addresses/{id} - Update address
        addresses.MapPut("/{id:guid}", async (
            [FromRoute] Guid id,
            [FromBody] UpdateAddressRequest request,
            [FromServices] IMediator mediator,
            ClaimsPrincipal user,
            CancellationToken ct) =>
        {
            // First get the address to check ownership
            var getQuery = new GetAddressQuery { AddressId = id };
            var getResult = await mediator.Send(getQuery, ct);

            if (!getResult.IsSuccess)
                return Results.NotFound(getResult.Error);

            // Check authorization
            if (!await IsAuthorizedForUser(getResult.Value!.UserId, user))
            {
                return Results.Forbid();
            }

            var command = new UpdateAddressCommand
            {
                AddressId = id,
                Street = request.Street,
                Street2 = request.Street2,
                City = request.City,
                State = request.State,
                PostalCode = request.PostalCode,
                Country = request.Country,
                Label = request.Label,
                IsDefault = request.IsDefault,
                PhoneNumbers = request.PhoneNumbers
            };

            var result = await mediator.Send(command, ct);

            return result.IsSuccess
                ? Results.Ok(new { success = true })
                : Results.BadRequest(result.Error);
        })
        .WithName("UpdateAddress")
        .WithSummary("Update address (User: own only, Admin: any address)")
        .Produces<object>(200)
        .Produces(403)
        .Produces(404)
        .Produces<string>(400);

        // DELETE /api/addresses/{id} - Soft delete address
        addresses.MapDelete("/{id:guid}", async (
            [FromRoute] Guid id,
            [FromServices] IMediator mediator,
            ClaimsPrincipal user,
            CancellationToken ct) =>
        {
            // First get the address to check ownership
            var getQuery = new GetAddressQuery { AddressId = id };
            var getResult = await mediator.Send(getQuery, ct);

            if (!getResult.IsSuccess)
                return Results.NotFound(getResult.Error);

            // Check authorization
            if (!await IsAuthorizedForUser(getResult.Value!.UserId, user))
            {
                return Results.Forbid();
            }

            var command = new DeleteAddressCommand { AddressId = id };
            var result = await mediator.Send(command, ct);

            return result.IsSuccess
                ? Results.Ok(new { success = true })
                : Results.BadRequest(result.Error);
        })
        .WithName("DeleteAddress")
        .WithSummary("Soft delete address (User: own only, Admin: any address)")
        .Produces<object>(200)
        .Produces(403)
        .Produces(404)
        .Produces<string>(400);
    }

    /// <summary>
    /// Checks if the current user is authorized to access resources for the specified userId.
    /// Users can only access their own resources, Admins can access any.
    /// </summary>
    private static Task<bool> IsAuthorizedForUser(Guid userId, ClaimsPrincipal user)
    {
        // Get current user ID from claims
        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier) ?? user.FindFirst("sub");
        if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var currentUserId))
        {
            return Task.FromResult(false);
        }

        // Check if user is admin
        var isAdmin = user.IsInRole(UserRole.Admin.ToString());

        // Admin can access any user, regular user can only access own resources
        var isAuthorized = isAdmin || currentUserId == userId;

        return Task.FromResult(isAuthorized);
    }
}

// Request DTOs for Address endpoints
public record CreateAddressRequest(
    Guid UserId,
    string Street,
    string City,
    string State,
    string PostalCode,
    string Country,
    string? Street2 = null,
    string? Label = null,
    bool? IsDefault = null,
    List<string>? PhoneNumbers = null);

public record UpdateAddressRequest(
    string? Street = null,
    string? Street2 = null,
    string? City = null,
    string? State = null,
    string? PostalCode = null,
    string? Country = null,
    string? Label = null,
    bool? IsDefault = null,
    List<string>? PhoneNumbers = null);
