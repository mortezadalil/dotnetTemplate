using Application.Common.Interfaces;
using Infrastructure.Authentication;
using Infrastructure.Caching;
using Infrastructure.Configuration;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure;

/// <summary>
/// Extension method to register all Infrastructure layer services.
/// This is called from the API project's Program.cs.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Initialize static AppConfig with appsettings values
        AppConfig.Initialize(configuration);

        // ========================================
        // CQRS DATABASE SETUP
        // ========================================

        // COMMAND DATABASE - For writes (source of truth)
        services.AddDbContext<CommandDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString("CommandConnection")
                ?? configuration.GetConnectionString("DefaultConnection")
                ?? "Data Source=command.db";

            options.UseSqlite(connectionString);

            if (configuration["ASPNETCORE_ENVIRONMENT"] == "Development")
            {
                options.EnableSensitiveDataLogging();
                options.EnableDetailedErrors();
            }
        });

        // QUERY DATABASE - For reads (optimized for queries)
        services.AddDbContext<QueryDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString("QueryConnection")
                ?? configuration.GetConnectionString("DefaultConnection")
                ?? "Data Source=query.db";

            options.UseSqlite(connectionString);
            options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking); // Optimize for reads

            if (configuration["ASPNETCORE_ENVIRONMENT"] == "Development")
            {
                options.EnableSensitiveDataLogging();
                options.EnableDetailedErrors();
            }
        });

        // Repository pattern
        // Default IUnitOfWork uses CommandDbContext (for command handlers)
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // Named registration for QueryUnitOfWork (for query handlers that need it)
        services.AddScoped<QueryUnitOfWork>();

        // Redis Cache
        var redisConnection = configuration.GetConnectionString("Redis");
        if (!string.IsNullOrEmpty(redisConnection))
        {
            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = redisConnection;
                options.InstanceName = "CleanArchTemplate_";
            });
        }
        else
        {
            // Fallback to in-memory cache if Redis not configured
            services.AddDistributedMemoryCache();
        }

        services.AddScoped<ICacheService, RedisCacheService>();

        // Authentication
        services.AddScoped<IJwtTokenService, JwtTokenService>();

        // Background Services
        services.AddHostedService<AppConfigRefreshService>();

        return services;
    }

    /// <summary>
    /// Ensures databases are created and migrations are applied.
    /// Initializes both Command (write) and Query (read) databases.
    /// </summary>
    public static async Task InitializeDatabaseAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();

        // Initialize Command DB (source of truth)
        var commandContext = scope.ServiceProvider.GetRequiredService<CommandDbContext>();
        await commandContext.Database.MigrateAsync();
        await SeedDataAsync(commandContext);

        // Initialize Query DB (read replica)
        var queryContext = scope.ServiceProvider.GetRequiredService<QueryDbContext>();
        await queryContext.Database.MigrateAsync();

        // Query DB will be populated via domain events when Command DB is written to
        // But for initial setup, copy existing data from Command DB
        if (!await queryContext.Users.AnyAsync() && await commandContext.Users.AnyAsync())
        {
            // Initial sync - in production, this would be handled by a migration or bulk sync job
            // For now, domain events will handle ongoing synchronization
        }
    }

    private static async Task SeedDataAsync(CommandDbContext context)
    {
        // Seed initial configurations if the table is empty
        if (!await context.Configs.AnyAsync())
        {
            var configs = new[]
            {
                Domain.Entities.Config.Create(
                    "Features.EnableNewUI",
                    "true",
                    "Enable the new UI redesign",
                    "Features"),

                Domain.Entities.Config.Create(
                    "Limits.MaxUploadSizeMB",
                    "50",
                    "Maximum file upload size in megabytes",
                    "Limits"),

                Domain.Entities.Config.Create(
                    "Maintenance.Mode",
                    "false",
                    "Enable maintenance mode",
                    "Maintenance"),

                Domain.Entities.Config.Create(
                    "Email.SenderAddress",
                    "noreply@example.com",
                    "Email address for outgoing emails",
                    "Email")
            };

            await context.Configs.AddRangeAsync(configs);
            await context.SaveChangesAsync();
        }
    }
}
