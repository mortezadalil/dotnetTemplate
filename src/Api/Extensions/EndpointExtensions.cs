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
        app.MapUserEndpoints();
        app.MapConfigEndpoints();
        app.MapAdminEndpoints();

        // Add more endpoint groups here as your API grows
        // app.MapProductEndpoints();
        // app.MapOrderEndpoints();
        // etc.

        return app;
    }
}
