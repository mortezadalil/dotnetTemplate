using Domain.Common;

namespace Domain.Entities;

/// <summary>
/// Stores domain events for reliable processing via the Outbox Pattern.
/// This ensures events are persisted before being published, preventing data loss.
/// </summary>
public class OutboxEvent : BaseEntity
{
    /// <summary>
    /// Type of the domain event (e.g., "UserCreatedEvent").
    /// </summary>
    public string EventType { get; private set; } = string.Empty;

    /// <summary>
    /// Serialized event data as JSON.
    /// </summary>
    public string EventData { get; private set; } = string.Empty;

    /// <summary>
    /// Number of times processing has been attempted.
    /// </summary>
    public int RetryCount { get; private set; }

    /// <summary>
    /// When the event was processed successfully.
    /// </summary>
    public DateTime? ProcessedAt { get; private set; }

    /// <summary>
    /// Error message from last failed attempt.
    /// </summary>
    public string? LastError { get; private set; }

    /// <summary>
    /// Next scheduled retry time (for exponential backoff).
    /// </summary>
    public DateTime? NextRetryAt { get; private set; }

    /// <summary>
    /// Private constructor for EF Core.
    /// </summary>
    private OutboxEvent() { }

    /// <summary>
    /// Creates a new outbox event.
    /// </summary>
    public static OutboxEvent Create(string eventType, string eventData)
    {
        if (string.IsNullOrWhiteSpace(eventType))
            throw new ArgumentException("Event type cannot be empty", nameof(eventType));

        if (string.IsNullOrWhiteSpace(eventData))
            throw new ArgumentException("Event data cannot be empty", nameof(eventData));

        return new OutboxEvent
        {
            EventType = eventType,
            EventData = eventData,
            RetryCount = 0,
            NextRetryAt = DateTime.UtcNow // Process immediately
        };
    }

    /// <summary>
    /// Marks the event as successfully processed.
    /// </summary>
    public void MarkAsProcessed()
    {
        ProcessedAt = DateTime.UtcNow;
        ModifiedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Records a failed processing attempt with exponential backoff.
    /// </summary>
    public void RecordFailure(string error)
    {
        RetryCount++;
        LastError = error;
        ModifiedAt = DateTime.UtcNow;

        // Exponential backoff: 2^retryCount seconds (2s, 4s, 8s, 16s, 32s, etc.)
        var delaySeconds = Math.Pow(2, RetryCount);
        NextRetryAt = DateTime.UtcNow.AddSeconds(delaySeconds);
    }

    /// <summary>
    /// Checks if the event has exceeded max retry attempts.
    /// </summary>
    public bool HasExceededMaxRetries(int maxRetries = 5)
    {
        return RetryCount >= maxRetries;
    }

    /// <summary>
    /// Checks if the event is ready to be retried.
    /// </summary>
    public bool IsReadyForRetry()
    {
        return ProcessedAt == null &&
               NextRetryAt.HasValue &&
               NextRetryAt.Value <= DateTime.UtcNow;
    }
}
