using Domain.Common;
using Domain.Events;

namespace Domain.Entities;

/// <summary>
/// Represents a configuration entry in the system.
/// This allows for database-driven configuration that can be changed without redeployment.
/// Raises domain events for CQRS synchronization.
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
    /// Raises ConfigCreatedEvent for CQRS synchronization.
    /// </summary>
    public static Config Create(string key, string value, string description = "", string category = "General")
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Config key cannot be empty", nameof(key));

        var config = new Config
        {
            Key = key,
            Value = value ?? string.Empty,
            Description = description,
            Category = category,
            IsActive = true
        };

        // Raise domain event for synchronization to read database
        config.AddDomainEvent(new ConfigCreatedEvent
        {
            ConfigId = config.Id,
            Key = config.Key,
            Value = config.Value,
            Description = config.Description,
            Category = config.Category,
            IsActive = config.IsActive,
            CreatedAt = config.CreatedAt
        });

        return config;
    }

    /// <summary>
    /// Updates the configuration value.
    /// Raises ConfigUpdatedEvent for CQRS synchronization.
    /// </summary>
    public void UpdateValue(string newValue)
    {
        Value = newValue ?? string.Empty;
        ModifiedAt = DateTime.UtcNow;
        RaiseUpdatedEvent();
    }

    /// <summary>
    /// Updates the configuration description.
    /// Raises ConfigUpdatedEvent for CQRS synchronization.
    /// </summary>
    public void UpdateDescription(string newDescription)
    {
        Description = newDescription;
        ModifiedAt = DateTime.UtcNow;
        RaiseUpdatedEvent();
    }

    /// <summary>
    /// Activates the configuration.
    /// Raises ConfigUpdatedEvent for CQRS synchronization.
    /// </summary>
    public void Activate()
    {
        IsActive = true;
        ModifiedAt = DateTime.UtcNow;
        RaiseUpdatedEvent();
    }

    /// <summary>
    /// Deactivates the configuration.
    /// Raises ConfigUpdatedEvent for CQRS synchronization.
    /// </summary>
    public void Deactivate()
    {
        IsActive = false;
        ModifiedAt = DateTime.UtcNow;
        RaiseUpdatedEvent();
    }

    /// <summary>
    /// Marks the config for deletion.
    /// Raises ConfigDeletedEvent for CQRS synchronization.
    /// </summary>
    public void Delete()
    {
        AddDomainEvent(new ConfigDeletedEvent
        {
            ConfigId = Id
        });
    }

    private void RaiseUpdatedEvent()
    {
        AddDomainEvent(new ConfigUpdatedEvent
        {
            ConfigId = Id,
            Key = Key,
            Value = Value,
            Description = Description,
            Category = Category,
            IsActive = IsActive
        });
    }
}
