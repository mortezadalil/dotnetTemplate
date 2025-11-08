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

        // Database
        services.AddDbContext<ApplicationDbContext>(options =>
        {
            // Using SQLite for simplicity - switch to SQL Server or PostgreSQL in production
            var connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? "Data Source=app.db";

            options.UseSqlite(connectionString);

            // Enable detailed errors in development
            if (configuration["ASPNETCORE_ENVIRONMENT"] == "Development")
            {
                options.EnableSensitiveDataLogging();
                options.EnableDetailedErrors();
            }
        });

        // Repository pattern
        services.AddScoped<IApplicationDbContext>(provider =>
            provider.GetRequiredService<ApplicationDbContext>());
        services.AddScoped<IUnitOfWork, UnitOfWork>();

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
    /// Ensures database is created and migrations are applied.
    /// </summary>
    public static async Task InitializeDatabaseAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Apply migrations
        await context.Database.MigrateAsync();

        // Seed data if needed
        await SeedDataAsync(context);
    }

    private static async Task SeedDataAsync(ApplicationDbContext context)
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
