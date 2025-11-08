using Application.Users.Commands.CreateUser;
using Application.Users.Commands.LoginUser;
using Application.Users.Queries.GetUser;
using Application.Users.Queries.GetUsers;
using MediatR;
using Microsoft.AspNetCore.Authorization;

namespace Api.Endpoints;

/// <summary>
/// User-related endpoints.
/// Each endpoint group is self-contained and focused on a single resource.
/// </summary>
public static class UserEndpoints
{
    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/users")
            .WithTags("Users")
            .WithOpenApi();

        // POST /api/users/register - Create new user
        group.MapPost("/register", async (CreateUserCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);

            return result.IsSuccess
                ? Results.Created($"/api/users/{result.Value}", new { userId = result.Value })
                : Results.BadRequest(new { error = result.Error });
        })
        .WithName("RegisterUser")
        .WithSummary("Register a new user")
        .AllowAnonymous();

        // POST /api/users/login - Authenticate user
        group.MapPost("/login", async (LoginCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);

            return result.IsSuccess
                ? Results.Ok(result.Value)
                : Results.BadRequest(new { error = result.Error });
        })
        .WithName("LoginUser")
        .WithSummary("Authenticate user and get JWT token")
        .AllowAnonymous();

        // GET /api/users/{id} - Get user by ID
        group.MapGet("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var query = new GetUserQuery { UserId = id };
            var result = await sender.Send(query);

            return result.IsSuccess
                ? Results.Ok(result.Value)
                : Results.NotFound(new { error = result.Error });
        })
        .WithName("GetUser")
        .WithSummary("Get user by ID")
        .RequireAuthorization();

        // GET /api/users - Get paginated list of users
        group.MapGet("/", async (
            int pageNumber,
            int pageSize,
            string? searchTerm,
            ISender sender) =>
        {
            var query = new GetUsersQuery
            {
                PageNumber = pageNumber == 0 ? 1 : pageNumber,
                PageSize = pageSize == 0 ? 10 : pageSize,
                SearchTerm = searchTerm
            };

            var result = await sender.Send(query);

            return result.IsSuccess
                ? Results.Ok(result.Value)
                : Results.BadRequest(new { error = result.Error });
        })
        .WithName("GetUsers")
        .WithSummary("Get paginated list of users")
        .RequireAuthorization();

        return app;
    }
}
