# Event Handler Error Handling - Critical Design Issue

## The Problem

When `CommandDbContext.SaveChangesAsync()` publishes domain events, there's a critical window for failure:

```csharp
// Line 72: Command DB saves and commits
var result = await base.SaveChangesAsync(cancellationToken); // ✅ COMMITTED

// Line 78: Event handler syncs to Query DB
await _mediator.Publish(domainEvent, cancellationToken); // ❌ Could fail!
```

**Issue:** Command DB is already committed. If event handler fails, you CANNOT rollback Command DB.

## Current Inconsistent Behavior

### UserCreatedEventHandler - Swallows Exceptions

```csharp
catch (Exception ex)
{
    _logger.LogError(ex, "Failed to synchronize...");
    // NO THROW - Exception is swallowed
}
```

**Result if handler fails:**
- ✅ Command DB: User created
- ❌ Query DB: User NOT created
- ✅ API returns success to client
- ⚠️ **Silent data inconsistency!**

### ConfigCreatedEventHandler - Rethrows Exceptions

```csharp
catch (Exception ex)
{
    _logger.LogError(ex, "Failed to sync...");
    throw; // Rethrows!
}
```

**Result if handler fails:**
- ✅ Command DB: Config created (already committed, cannot rollback!)
- ❌ Query DB: Config NOT created
- ❌ API returns error to client
- ⚠️ **Visible data inconsistency + User sees error even though write succeeded!**

## Why Both Approaches Are Problematic

| Approach | Command DB | Query DB | API Response | Issue |
|----------|------------|----------|--------------|-------|
| **Swallow Exception** | ✅ Updated | ❌ Not Updated | ✅ Success | Silent failure - data out of sync |
| **Rethrow Exception** | ✅ Updated | ❌ Not Updated | ❌ Error | Confusing - user sees error but write succeeded |

## Solution 1: Eventual Consistency with Retry (Recommended)

Accept that failures can happen and build resilience:

### Implementation

```csharp
public class UserCreatedEventHandler : INotificationHandler<UserCreatedEvent>
{
    private readonly QueryDbContext _queryDb;
    private readonly ILogger<UserCreatedEventHandler> _logger;
    private readonly IEventRetryQueue _retryQueue; // New dependency

    public async Task Handle(UserCreatedEvent notification, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Syncing user to Query DB: {UserId}", notification.UserId);

            // Sync to Query DB
            await SyncUserToQueryDb(notification, cancellationToken);

            _logger.LogInformation("Successfully synced user {UserId}", notification.UserId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to sync user {UserId}. Queueing for retry.", notification.UserId);

            // Queue for retry instead of failing silently
            await _retryQueue.EnqueueAsync(notification, cancellationToken);

            // Don't throw - Command DB already committed
            // Let the retry mechanism handle eventual consistency
        }
    }
}
```

### Benefits
- ✅ Command DB always succeeds
- ✅ Eventual consistency via retry queue
- ✅ User sees success immediately
- ✅ Background process retries failed syncs
- ✅ Can monitor retry queue for failures

### Implementation Options

**Option A: In-Memory Queue with Background Service**
```csharp
public interface IEventRetryQueue
{
    Task EnqueueAsync<TEvent>(TEvent @event, CancellationToken cancellationToken) where TEvent : IDomainEvent;
}

public class EventRetryBackgroundService : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            // Dequeue and retry failed events
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }
    }
}
```

**Option B: Use Outbox Pattern**
```csharp
// Store failed events in Command DB
public class OutboxEvent
{
    public Guid Id { get; set; }
    public string EventType { get; set; }
    public string EventData { get; set; } // JSON
    public int RetryCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
}

// Background service processes outbox
// Retries with exponential backoff
// Moves to dead-letter queue after N failures
```

**Option C: Use Message Queue (RabbitMQ, Azure Service Bus)**
```csharp
catch (Exception ex)
{
    await _messageQueue.PublishAsync(notification, new PublishOptions
    {
        DelaySeconds = 5,
        MaxRetries = 3
    });
}
```

## Solution 2: Two-Phase Commit (Complex, Not Recommended)

Use distributed transactions across both databases.

### Implementation

```csharp
using var scope = new TransactionScope(
    TransactionScopeAsyncFlowOption.Enabled);

await _commandDb.SaveChangesAsync();
await _queryDb.SaveChangesAsync();

scope.Complete(); // Both commit or both rollback
```

### Issues
- ❌ Complex and error-prone
- ❌ Performance overhead
- ❌ SQLite doesn't support distributed transactions
- ❌ Goes against CQRS eventual consistency model
- ❌ Tight coupling between databases

## Solution 3: Save Events to Outbox Before Publishing (Best Practice)

Store domain events in Command DB before publishing.

### Implementation

```csharp
public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
{
    // Collect domain events
    var domainEvents = ChangeTracker.Entries<BaseEntity>()
        .SelectMany(e => e.DomainEvents)
        .ToList();

    // Save events to outbox table in Command DB
    foreach (var domainEvent in domainEvents)
    {
        OutboxEvents.Add(new OutboxEvent
        {
            EventType = domainEvent.GetType().Name,
            EventData = JsonSerializer.Serialize(domainEvent),
            CreatedAt = DateTime.UtcNow
        });
    }

    // Save changes to Command DB (includes outbox events)
    var result = await base.SaveChangesAsync(cancellationToken);

    // Try to publish events
    foreach (var domainEvent in domainEvents)
    {
        try
        {
            await _mediator.Publish(domainEvent, cancellationToken);
            // Mark outbox event as processed
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish event. Will retry from outbox.");
            // Don't throw - event is in outbox, background service will retry
        }
    }

    return result;
}
```

### Benefits
- ✅ Events are persisted before publishing (no event loss)
- ✅ Background service can retry from outbox
- ✅ Command DB is source of truth for events
- ✅ Can audit all events
- ✅ Can replay events if needed

## Solution 4: Monitor and Alert (Minimum Viable)

If implementing retry is too complex initially:

```csharp
catch (Exception ex)
{
    _logger.LogError(ex, "CRITICAL: Failed to sync user {UserId} to Query DB", notification.UserId);

    // Send alert to monitoring system
    await _alertService.SendCriticalAlertAsync(
        "CQRS Sync Failure",
        $"User {notification.UserId} not synced to Query DB",
        ex);

    // Don't throw - let operation succeed
    // Ops team will manually sync from alerts
}
```

## Recommended Approach

**For Production:**
1. Implement **Outbox Pattern** (Solution 3)
2. Add **Background Service** to process outbox with retry logic
3. Add **Dead Letter Queue** for events that fail after N retries
4. Add **Monitoring and Alerts** for failed events
5. Implement **Manual Sync Tool** for ops team

**For Development/Simple Cases:**
1. Swallow exceptions in all handlers (consistent behavior)
2. Add detailed logging
3. Add monitoring/alerting
4. Document that Query DB has eventual consistency
5. Accept that rare failures may require manual intervention

## Current Code Recommendations

### Fix 1: Make All Handlers Consistent (Quick Fix)

All handlers should either:
- **Swallow exceptions** (accept eventual consistency)
- OR **Rethrow exceptions** (fail fast, but confusing to users)

I recommend **swallow + log + alert** for now:

```csharp
catch (Exception ex)
{
    _logger.LogError(ex,
        "CRITICAL: Failed to sync {Entity} {Id} to Query DB. Manual intervention may be required.",
        nameof(Config), notification.ConfigId);

    // TODO: Implement retry queue
    // For now, don't throw - Command DB already committed
}
```

### Fix 2: Add Monitoring

Create a health check that compares record counts:

```csharp
public class DatabaseSyncHealthCheck : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context)
    {
        var commandUserCount = await _commandDb.Users.CountAsync();
        var queryUserCount = await _queryDb.Users.CountAsync();

        var diff = Math.Abs(commandUserCount - queryUserCount);

        if (diff > 10) // Threshold
        {
            return HealthCheckResult.Unhealthy(
                $"User count mismatch: Command={commandUserCount}, Query={queryUserCount}");
        }

        return HealthCheckResult.Healthy();
    }
}
```

## Summary

**The Fundamental Problem:**
You cannot have ACID transactions across two separate databases in this architecture.

**Accept:**
Eventual consistency is the trade-off of CQRS with separate databases.

**Mitigation:**
1. Swallow exceptions consistently
2. Log failures prominently
3. Implement retry mechanism
4. Monitor for sync failures
5. Build manual sync tools for edge cases

This is the reality of distributed systems!

---

## ✅ IMPLEMENTED SOLUTION: Outbox Pattern

**This project now implements the Outbox Pattern to guarantee eventual consistency.**

See [OUTBOX_PATTERN_IMPLEMENTATION.md](./OUTBOX_PATTERN_IMPLEMENTATION.md) for complete documentation.

### Quick Summary

**Before Outbox Pattern:**
```csharp
await base.SaveChangesAsync();      // ✅ Committed
await _mediator.Publish(event);     // ❌ If fails → event LOST
```

**After Outbox Pattern:**
```csharp
// Save event to outbox table
await OutboxEvents.AddAsync(outboxEvent);
await base.SaveChangesAsync();      // ✅ Business data + Event committed atomically

try {
    await _mediator.Publish(event); // Try immediate publish
    outboxEvent.MarkAsProcessed();  // ✅ Success
} catch {
    outboxEvent.RecordFailure();    // ⚠️ Will retry from outbox
}
```

### What Changed

1. **OutboxEvent Entity**: Stores events before publishing
2. **CommandDbContext**: Persists events in same transaction as business data
3. **OutboxProcessor**: Background service retries failed events every 10 seconds
4. **Exponential Backoff**: 2s, 4s, 8s, 16s, 32s retry delays
5. **Dead Letter Queue**: Events failing 5+ times are marked for manual review

### Benefits

- ✅ **Zero Event Loss**: Events persisted before publishing
- ✅ **Guaranteed Eventual Consistency**: Failed events retry automatically
- ✅ **Graceful Degradation**: System works even if Query DB is down
- ✅ **Full Audit Trail**: All events tracked in database
- ✅ **Observable**: Monitor queue size, retry rates, dead letters

### What This Means for You

**Event publishing can no longer fail silently.** Every domain event is:
1. Persisted to outbox (guaranteed)
2. Published immediately (optimistic attempt)
3. Retried automatically if publishing fails (guaranteed eventual success)
4. Moved to dead letter queue if permanently failing (manual intervention)

**Command DB and Query DB will always sync eventually**, even through:
- Network failures
- Database downtime
- Application restarts
- Temporary infrastructure issues

This is the production-ready solution for CQRS with separate databases.
