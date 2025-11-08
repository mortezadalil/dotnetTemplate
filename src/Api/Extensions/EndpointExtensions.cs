using Api.Endpoints;

namespace Api.Extensions;

/// <summary>
/// Extension methods for registering all endpoint groups.
/// Makes Program.cs clean and organized.
/// </summary>
public static class EndpointExtensions
{
    public static IEndpointRouteBuilder MapEndpoints(this IEndpointRouteBuilder app)
    {
        // Public endpoints
        app.MapUserEndpoints();
        app.MapConfigEndpoints();

        // Address endpoints (require authentication, multi-role)
        app.MapAddressEndpoints();

        // Admin endpoints (require Admin role)
        app.MapAdminUsersEndpoints();
        app.MapAdminConfigsEndpoints();

        // Add more endpoint groups here as your API grows
        // app.MapProductEndpoints();
        // app.MapOrderEndpoints();
        // etc.

        return app;
    }
}
