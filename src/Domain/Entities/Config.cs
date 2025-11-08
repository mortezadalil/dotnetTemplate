using Domain.Common;

namespace Domain.Entities;

/// <summary>
/// Represents a configuration entry in the system.
/// This allows for database-driven configuration that can be changed without redeployment.
/// </summary>
public class Config : BaseEntity
{
    /// <summary>
    /// Configuration key. Must be unique.
    /// Example: "Features.EnableNewUI", "Limits.MaxUploadSizeMB"
    /// </summary>
    public string Key { get; private set; } = string.Empty;

    /// <summary>
    /// Configuration value as a string.
    /// Can represent any type - parsing happens at the application layer.
    /// </summary>
    public string Value { get; private set; } = string.Empty;

    /// <summary>
    /// Description of what this configuration controls.
    /// </summary>
    public string Description { get; private set; } = string.Empty;

    /// <summary>
    /// Category for grouping related configs.
    /// Example: "Features", "Limits", "Integrations"
    /// </summary>
    public string Category { get; private set; } = string.Empty;

    /// <summary>
    /// Whether this configuration is currently active.
    /// </summary>
    public bool IsActive { get; private set; }

    /// <summary>
    /// Private constructor for EF Core.
    /// </summary>
    private Config() { }

    /// <summary>
    /// Creates a new configuration entry.
    /// </summary>
    public static Config Create(string key, string value, string description = "", string category = "General")
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Config key cannot be empty", nameof(key));

        return new Config
        {
            Key = key,
            Value = value ?? string.Empty,
            Description = description,
            Category = category,
            IsActive = true
        };
    }

    /// <summary>
    /// Updates the configuration value.
    /// </summary>
    public void UpdateValue(string newValue)
    {
        Value = newValue ?? string.Empty;
        ModifiedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Updates the configuration description.
    /// </summary>
    public void UpdateDescription(string newDescription)
    {
        Description = newDescription;
        ModifiedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Activates the configuration.
    /// </summary>
    public void Activate()
    {
        IsActive = true;
        ModifiedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Deactivates the configuration.
    /// </summary>
    public void Deactivate()
    {
        IsActive = false;
        ModifiedAt = DateTime.UtcNow;
    }
}
