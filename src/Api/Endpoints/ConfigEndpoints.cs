using Application.Configs.Queries.GetConfigs;
using Infrastructure.Configuration;
using MediatR;
using Microsoft.AspNetCore.Authorization;

namespace Api.Endpoints;

/// <summary>
/// Configuration-related endpoints.
/// Demonstrates how to expose AppConfig values and database configs.
/// </summary>
public static class ConfigEndpoints
{
    public static IEndpointRouteBuilder MapConfigEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/configs")
            .WithTags("Configurations")
            .WithOpenApi();

        // GET /api/configs - Get all database configs
        group.MapGet("/", async (ISender sender) =>
        {
            var query = new GetConfigsQuery { OnlyActive = true };
            var result = await sender.Send(query);

            return result.IsSuccess
                ? Results.Ok(result.Value)
                : Results.BadRequest(new { error = result.Error });
        })
        .WithName("GetConfigs")
        .WithSummary("Get all active configurations from database")
        .RequireAuthorization();

        // GET /api/configs/{key} - Get specific config by key
        group.MapGet("/{key}", (string key) =>
        {
            var value = AppConfig.Get(key);

            if (string.IsNullOrEmpty(value))
                return Results.NotFound(new { error = $"Configuration '{key}' not found" });

            return Results.Ok(new { key, value });
        })
        .WithName("GetConfigByKey")
        .WithSummary("Get specific configuration value by key")
        .RequireAuthorization();

        // GET /api/configs/all/keys - Get all config keys
        group.MapGet("/all/keys", () =>
        {
            var keys = AppConfig.GetAllKeys();
            return Results.Ok(new { keys, count = keys.Count() });
        })
        .WithName("GetAllConfigKeys")
        .WithSummary("Get all configuration keys")
        .RequireAuthorization();

        // GET /api/configs/info - Get AppConfig metadata
        group.MapGet("/info", () =>
        {
            return Results.Ok(new
            {
                lastRefreshTime = AppConfig.LastRefreshTime,
                environment = AppConfig.Environment,
                apiName = AppConfig.ApiName,
                totalConfigs = AppConfig.GetAllKeys().Count()
            });
        })
        .WithName("GetConfigInfo")
        .WithSummary("Get AppConfig metadata and refresh info")
        .AllowAnonymous();

        return app;
    }
}
