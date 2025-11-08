using System.Collections.Concurrent;
using System.Collections.Immutable;

namespace Infrastructure.Configuration;

/// <summary>
/// THE MAGIC: Static configuration class that combines appsettings.json and database configs.
/// Automatically refreshes every 1 minute via AppConfigRefreshService.
/// Thread-safe and blazing fast.
///
/// Usage anywhere in your code:
///   var value = AppConfig.JwtSecretKey;  // From appsettings
///   var feature = AppConfig.Get("Features.EnableNewUI");  // From database
/// </summary>
public static class AppConfig
{
    private static ImmutableDictionary<string, string> _databaseConfigs =
        ImmutableDictionary<string, string>.Empty;

    private static readonly object _lock = new();

    // ========================================
    // APPSETTINGS.JSON VALUES (Static Properties)
    // ========================================

    /// <summary>
    /// JWT Secret Key from appsettings
    /// </summary>
    public static string JwtSecretKey { get; private set; } = string.Empty;

    /// <summary>
    /// JWT Issuer from appsettings
    /// </summary>
    public static string JwtIssuer { get; private set; } = string.Empty;

    /// <summary>
    /// JWT Audience from appsettings
    /// </summary>
    public static string JwtAudience { get; private set; } = string.Empty;

    /// <summary>
    /// JWT Token expiration in minutes
    /// </summary>
    public static int JwtExpirationMinutes { get; private set; } = 60;

    /// <summary>
    /// Database connection string
    /// </summary>
    public static string ConnectionString { get; private set; } = string.Empty;

    /// <summary>
    /// Redis connection string
    /// </summary>
    public static string RedisConnectionString { get; private set; } = string.Empty;

    /// <summary>
    /// Seq server URL
    /// </summary>
    public static string SeqServerUrl { get; private set; } = string.Empty;

    /// <summary>
    /// API name/title
    /// </summary>
    public static string ApiName { get; private set; } = "Clean Architecture API";

    /// <summary>
    /// Environment name
    /// </summary>
    public static string Environment { get; private set; } = "Development";

    // ========================================
    // DATABASE CONFIGS (Dynamic from Configs table)
    // ========================================

    /// <summary>
    /// Gets a configuration value from the database Configs table.
    /// </summary>
    /// <param name="key">Configuration key</param>
    /// <param name="defaultValue">Default value if key not found</param>
    /// <returns>Configuration value or default</returns>
    public static string Get(string key, string defaultValue = "")
    {
        return _databaseConfigs.TryGetValue(key, out var value) ? value : defaultValue;
    }

    /// <summary>
    /// Gets a configuration value as integer.
    /// </summary>
    public static int GetInt(string key, int defaultValue = 0)
    {
        var value = Get(key);
        return int.TryParse(value, out var result) ? result : defaultValue;
    }

    /// <summary>
    /// Gets a configuration value as boolean.
    /// </summary>
    public static bool GetBool(string key, bool defaultValue = false)
    {
        var value = Get(key);
        return bool.TryParse(value, out var result) ? result : defaultValue;
    }

    /// <summary>
    /// Gets a configuration value as decimal.
    /// </summary>
    public static decimal GetDecimal(string key, decimal defaultValue = 0m)
    {
        var value = Get(key);
        return decimal.TryParse(value, out var result) ? result : defaultValue;
    }

    /// <summary>
    /// Gets all database configuration keys.
    /// </summary>
    public static IEnumerable<string> GetAllKeys()
    {
        return _databaseConfigs.Keys;
    }

    /// <summary>
    /// Gets all database configurations as a dictionary (immutable).
    /// </summary>
    public static IReadOnlyDictionary<string, string> GetAll()
    {
        return _databaseConfigs;
    }

    // ========================================
    // INTERNAL METHODS (Used by AppConfigRefreshService)
    // ========================================

    /// <summary>
    /// Initializes AppConfig with values from appsettings.json.
    /// Called once at application startup.
    /// </summary>
    internal static void Initialize(Microsoft.Extensions.Configuration.IConfiguration configuration)
    {
        lock (_lock)
        {
            JwtSecretKey = configuration["Jwt:SecretKey"] ?? string.Empty;
            JwtIssuer = configuration["Jwt:Issuer"] ?? "DotNetTemplate";
            JwtAudience = configuration["Jwt:Audience"] ?? "DotNetTemplateAPI";
            JwtExpirationMinutes = int.Parse(configuration["Jwt:ExpirationMinutes"] ?? "60");

            ConnectionString = configuration.GetConnectionString("DefaultConnection") ?? string.Empty;
            RedisConnectionString = configuration.GetConnectionString("Redis") ?? string.Empty;

            SeqServerUrl = configuration["Seq:ServerUrl"] ?? string.Empty;
            ApiName = configuration["Api:Name"] ?? "Clean Architecture API";
            Environment = configuration["ASPNETCORE_ENVIRONMENT"] ?? "Development";
        }
    }

    /// <summary>
    /// Refreshes database configurations.
    /// Called by AppConfigRefreshService every 1 minute.
    /// </summary>
    internal static void RefreshDatabaseConfigs(Dictionary<string, string> configs)
    {
        lock (_lock)
        {
            _databaseConfigs = configs.ToImmutableDictionary();
        }
    }

    /// <summary>
    /// Gets the last refresh timestamp (for monitoring).
    /// </summary>
    public static DateTime LastRefreshTime { get; internal set; } = DateTime.MinValue;
}
