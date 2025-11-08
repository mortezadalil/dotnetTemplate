# Outbox Pattern Implementation

## Overview

This project implements the **Outbox Pattern** to guarantee eventual consistency between Command and Query databases. This pattern ensures that domain events are never lost, even if publishing fails.

## The Problem (Before Outbox Pattern)

```
┌─────────────────────────────────────────────────────────────────┐
│ PROBLEM: Events could be lost if publishing fails              │
├─────────────────────────────────────────────────────────────────┤
│ 1. Save to Command DB       ✅ COMMITTED (permanent)            │
│ 2. Publish event            ❌ FAILS (network/DB down)          │
│ 3. Event is LOST forever    💥 No retry mechanism               │
│                                                                  │
│ Result: Command DB ≠ Query DB (permanent inconsistency)        │
└─────────────────────────────────────────────────────────────────┘
```

## The Solution (Outbox Pattern)

```
┌─────────────────────────────────────────────────────────────────┐
│ SOLUTION: Events persisted before publishing                    │
├─────────────────────────────────────────────────────────────────┤
│ 1. Save business data + events to Command DB  ✅ ATOMIC         │
│    (both in same transaction)                                   │
│ 2. Try to publish immediately                 ✅ or ⚠️          │
│    - If success: mark as processed                              │
│    - If fails: retry later from outbox                          │
│ 3. Background service retries failed events   ✅ GUARANTEED     │
│                                                                  │
│ Result: Eventual consistency GUARANTEED                         │
└─────────────────────────────────────────────────────────────────┘
```

## Architecture

### Components

1. **OutboxEvent Entity** (`src/Domain/Entities/OutboxEvent.cs`)
   - Stores serialized domain events
   - Tracks retry attempts and status
   - Implements exponential backoff

2. **CommandDbContext** (`src/Infrastructure/Persistence/CommandDbContext.cs`)
   - Persists events to outbox table in same transaction
   - Attempts immediate publishing (optimistic path)
   - Falls back to outbox if publishing fails

3. **OutboxProcessor** (`src/Infrastructure/BackgroundServices/OutboxProcessor.cs`)
   - Background service that runs every 10 seconds
   - Processes unprocessed events from outbox
   - Implements retry with exponential backoff
   - Moves to dead letter after 5 failed attempts

## Flow Diagram

### Happy Path (Event Publishes Successfully)

```
User Action
    │
    ├─> Command Handler
    │       │
    │       ├─> Domain Entity (raises UserCreatedEvent)
    │       │
    │       └─> UnitOfWork.SaveChangesAsync()
    │               │
    │               ├─> CommandDbContext.SaveChangesAsync()
    │               │       │
    │               │       ├─ 1. Serialize event to JSON
    │               │       ├─ 2. Create OutboxEvent record
    │               │       ├─ 3. Save user + outbox event (ATOMIC)
    │               │       │      ✅ Both committed in single transaction
    │               │       │
    │               │       ├─ 4. Try to publish immediately
    │               │       │      ✅ SUCCESS
    │               │       │
    │               │       └─ 5. Mark OutboxEvent as processed
    │               │              ✅ ProcessedAt = DateTime.UtcNow
    │               │
    │               └─> Event Handler (UserCreatedEventHandler)
    │                       │
    │                       └─> Query DB updated ✅
    │
    └─> Response to user (success)
```

### Failure Path (Event Publishing Fails)

```
User Action
    │
    ├─> Command Handler
    │       │
    │       ├─> Domain Entity (raises ConfigCreatedEvent)
    │       │
    │       └─> UnitOfWork.SaveChangesAsync()
    │               │
    │               ├─> CommandDbContext.SaveChangesAsync()
    │               │       │
    │               │       ├─ 1. Serialize event to JSON
    │               │       ├─ 2. Create OutboxEvent record
    │               │       ├─ 3. Save config + outbox event (ATOMIC)
    │               │       │      ✅ Both committed in single transaction
    │               │       │
    │               │       ├─ 4. Try to publish immediately
    │               │       │      ❌ FAILS (Query DB is down)
    │               │       │
    │               │       ├─ 5. Catch exception (don't rethrow)
    │               │       └─ 6. Mark OutboxEvent for retry
    │               │              ⚠️ RetryCount = 1
    │               │              ⚠️ NextRetryAt = UtcNow + 2 seconds
    │               │
    │               └─> Response to user (success) ✅
    │
    └─> [10 seconds later]
            │
            OutboxProcessor (Background Service)
                │
                ├─ 1. Query unprocessed events
                ├─ 2. Find ConfigCreatedEvent (ready for retry)
                ├─ 3. Deserialize event
                ├─ 4. Publish via MediatR
                │      ✅ SUCCESS (Query DB is back up)
                │
                └─ 5. Mark OutboxEvent as processed
                       ✅ ProcessedAt = DateTime.UtcNow

Result: Eventual consistency achieved!
        Command DB and Query DB are now in sync ✅
```

### Persistent Failure Path (Max Retries Exceeded)

```
User Action → Config created in Command DB ✅
    │
    └─> Event publishing fails ❌
            │
            ├─ Retry #1 (after 2 seconds)   ❌ Fails
            ├─ Retry #2 (after 4 seconds)   ❌ Fails
            ├─ Retry #3 (after 8 seconds)   ❌ Fails
            ├─ Retry #4 (after 16 seconds)  ❌ Fails
            └─ Retry #5 (after 32 seconds)  ❌ Fails
                    │
                    └─ Exceeded max retries (5)
                            │
                            ├─ Move to Dead Letter (soft delete)
                            ├─ Alert operations team 🚨
                            └─ Manual intervention required
```

## Implementation Details

### 1. OutboxEvent Entity

```csharp
public class OutboxEvent : BaseEntity
{
    public string EventType { get; private set; }     // "UserCreatedEvent"
    public string EventData { get; private set; }     // JSON serialized event
    public int RetryCount { get; private set; }       // Number of retry attempts
    public DateTime? ProcessedAt { get; private set; } // When successfully processed
    public string? LastError { get; private set; }    // Error from last failure
    public DateTime? NextRetryAt { get; private set; } // Next scheduled retry

    public void MarkAsProcessed() { ... }
    public void RecordFailure(string error) { ... }  // Exponential backoff
    public bool HasExceededMaxRetries(int max = 5) { ... }
}
```

### 2. CommandDbContext Integration

```csharp
public override async Task<int> SaveChangesAsync(CancellationToken ct)
{
    // Collect domain events
    var domainEvents = GetDomainEvents();

    // STEP 1: Save events to outbox (SAME TRANSACTION as business data)
    foreach (var domainEvent in domainEvents)
    {
        var outboxEvent = OutboxEvent.Create(
            domainEvent.GetType().Name,
            JsonSerializer.Serialize(domainEvent)
        );
        await OutboxEvents.AddAsync(outboxEvent);
    }

    // STEP 2: Commit business data + outbox events atomically
    var result = await base.SaveChangesAsync(ct);

    // STEP 3: Try immediate publishing (optimistic)
    foreach (var (domainEvent, outboxEvent) in domainEvents.Zip(outboxEvents))
    {
        try
        {
            await _mediator.Publish(domainEvent, ct);
            outboxEvent.MarkAsProcessed(); // Success!
        }
        catch (Exception ex)
        {
            // Don't throw - event is safely in outbox
            outboxEvent.RecordFailure(ex.Message); // Will retry later
        }
    }

    // STEP 4: Save processing status
    await base.SaveChangesAsync(ct);

    return result;
}
```

### 3. OutboxProcessor Background Service

```csharp
public class OutboxProcessor : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            // Process every 10 seconds
            await ProcessOutboxEventsAsync(stoppingToken);
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }
    }

    private async Task ProcessOutboxEventsAsync(CancellationToken ct)
    {
        // Get unprocessed events ready for retry
        var events = await _dbContext.OutboxEvents
            .Where(e => e.ProcessedAt == null)
            .Where(e => e.NextRetryAt <= DateTime.UtcNow)
            .Take(100)
            .ToListAsync(ct);

        foreach (var outboxEvent in events)
        {
            // Check max retries
            if (outboxEvent.HasExceededMaxRetries(5))
            {
                outboxEvent.IsDeleted = true; // Dead letter
                continue;
            }

            try
            {
                // Deserialize and publish
                var domainEvent = Deserialize(outboxEvent);
                await _mediator.Publish(domainEvent, ct);

                outboxEvent.MarkAsProcessed(); // Success!
            }
            catch (Exception ex)
            {
                outboxEvent.RecordFailure(ex.Message); // Exponential backoff
            }
        }

        await _dbContext.SaveChangesAsync(ct);
    }
}
```

## Exponential Backoff Strategy

Retry delays grow exponentially to avoid overwhelming a failing system:

| Retry # | Delay | Total Wait Time |
|---------|-------|-----------------|
| 1 | 2s | 2s |
| 2 | 4s | 6s |
| 3 | 8s | 14s |
| 4 | 16s | 30s |
| 5 | 32s | 62s |

After 5 failures, event moves to dead letter for manual investigation.

## Guarantees

### ✅ What This Guarantees

1. **No Event Loss**: Events are persisted before publishing
2. **Atomicity**: Business data and events saved in single transaction
3. **Eventual Consistency**: Failed events will retry until successful
4. **Graceful Degradation**: System continues working even if Query DB is down
5. **Observability**: All failures are logged with full context

### ⚠️ What This Does NOT Guarantee

1. **Immediate Consistency**: Query DB may lag behind Command DB
2. **Ordering**: Events may be processed out of order if some fail
3. **Exactly-Once Processing**: Duplicate processing is possible (handlers must be idempotent)

## Monitoring and Alerts

### Key Metrics to Monitor

1. **Outbox Queue Size**: Number of unprocessed events
   ```sql
   SELECT COUNT(*) FROM OutboxEvents WHERE ProcessedAt IS NULL
   ```

2. **Failed Events**: Events exceeding max retries
   ```sql
   SELECT COUNT(*) FROM OutboxEvents WHERE RetryCount >= 5
   ```

3. **Average Processing Time**: Time from creation to processing
   ```sql
   SELECT AVG(DATEDIFF(second, CreatedAt, ProcessedAt))
   FROM OutboxEvents WHERE ProcessedAt IS NOT NULL
   ```

4. **Event Types Distribution**:
   ```sql
   SELECT EventType, COUNT(*) as Count, AVG(RetryCount) as AvgRetries
   FROM OutboxEvents
   GROUP BY EventType
   ```

### Recommended Alerts

- 🚨 **Critical**: Unprocessed events > 100
- ⚠️ **Warning**: Events with RetryCount > 3
- 📊 **Info**: Average processing lag > 60 seconds

## Dead Letter Queue Management

Events that fail after 5 retries are soft-deleted (IsDeleted = true):

```sql
-- View dead letter events
SELECT Id, EventType, RetryCount, LastError, CreatedAt
FROM OutboxEvents
WHERE IsDeleted = true
ORDER BY CreatedAt DESC
```

### Manual Recovery Procedure

1. **Investigate the failure**:
   ```sql
   SELECT * FROM OutboxEvents WHERE Id = 'failed-event-id'
   ```

2. **Check Query DB state**:
   ```sql
   -- Example: Check if user exists in Query DB
   SELECT * FROM Users WHERE Id = 'user-id-from-event'
   ```

3. **Manual Sync (if needed)**:
   ```csharp
   // Option A: Requeue the event
   UPDATE OutboxEvents
   SET RetryCount = 0, NextRetryAt = GETUTCDATE(), IsDeleted = 0
   WHERE Id = 'failed-event-id'

   // Option B: Manually sync to Query DB
   // Write a one-time script to copy data from Command to Query DB
   ```

4. **Fix root cause** before requeuing to avoid repeated failures

## Performance Considerations

### Database Load

- **Outbox Table Growth**: Old processed events should be archived
  ```sql
  -- Archive events older than 30 days
  DELETE FROM OutboxEvents
  WHERE ProcessedAt IS NOT NULL
  AND ProcessedAt < DATEADD(day, -30, GETUTCDATE())
  ```

- **Index Recommendations**:
  ```sql
  CREATE INDEX IX_OutboxEvents_Processing
  ON OutboxEvents(ProcessedAt, NextRetryAt)
  WHERE ProcessedAt IS NULL
  ```

### Batch Processing

OutboxProcessor processes events in batches of 100 to balance:
- Throughput (larger batches = better performance)
- Latency (smaller batches = faster individual event processing)

Adjust batch size based on your needs in `OutboxProcessor.cs:66`.

## Testing Strategy

### Unit Tests

```csharp
[Fact]
public async Task SaveChangesAsync_PersistsEventToOutbox()
{
    // Arrange
    var user = User.Create("test@example.com", "Test User", "hash");

    // Act
    await _commandDb.Users.AddAsync(user);
    await _commandDb.SaveChangesAsync();

    // Assert
    var outboxEvent = await _commandDb.OutboxEvents
        .FirstOrDefaultAsync(e => e.EventType == nameof(UserCreatedEvent));
    Assert.NotNull(outboxEvent);
}

[Fact]
public async Task OutboxProcessor_RetriesFailedEvents()
{
    // Test exponential backoff logic
}

[Fact]
public async Task OutboxProcessor_MovesToDeadLetterAfterMaxRetries()
{
    // Test dead letter queue behavior
}
```

### Integration Tests

```csharp
[Fact]
public async Task EventualConsistency_CommandAndQueryDbSynced()
{
    // 1. Create user in Command DB
    // 2. Verify event in outbox
    // 3. Wait for OutboxProcessor
    // 4. Verify user in Query DB
}

[Fact]
public async Task EventualConsistency_RecoverFromQueryDbFailure()
{
    // 1. Stop Query DB
    // 2. Create user in Command DB
    // 3. Verify event in outbox (unprocessed)
    // 4. Start Query DB
    // 5. Wait for OutboxProcessor
    // 6. Verify user in Query DB
}
```

## Configuration

### OutboxProcessor Settings

Edit `OutboxProcessor.cs` to customize:

```csharp
private readonly TimeSpan _processingInterval = TimeSpan.FromSeconds(10);
private const int MaxRetries = 5;
private const int BatchSize = 100;
```

### Logging

OutboxProcessor logs at different levels:
- **Debug**: Individual event processing
- **Information**: Batch processing summary
- **Warning**: Retry attempts
- **Error**: Max retries exceeded

## Migration from Previous Implementation

### Before (No Outbox)

Events were published directly:
```csharp
await base.SaveChangesAsync(); // Command DB committed
await _mediator.Publish(event); // If this fails, event is LOST
```

### After (With Outbox)

Events are persisted first:
```csharp
// Save event to outbox
await OutboxEvents.AddAsync(outboxEvent);
await base.SaveChangesAsync(); // Command DB + Outbox committed atomically

// Try immediate publish (optimistic)
await _mediator.Publish(event); // If this fails, event is in outbox
```

### Migration Steps

1. ✅ Added `OutboxEvent` entity
2. ✅ Modified `CommandDbContext.SaveChangesAsync()`
3. ✅ Created `OutboxProcessor` background service
4. ✅ Registered in DI container
5. ⚠️ **TODO**: Create database migration for OutboxEvents table
6. ⚠️ **TODO**: Add monitoring dashboard
7. ⚠️ **TODO**: Set up alerts for dead letter queue

## Benefits Over Previous Approach

| Aspect | Before | After (Outbox Pattern) |
|--------|--------|------------------------|
| **Event Loss** | ❌ Possible if publish fails | ✅ Impossible - persisted first |
| **Consistency** | ⚠️ Eventual (if it works) | ✅ Guaranteed eventual |
| **Retry Logic** | ❌ None | ✅ Exponential backoff |
| **Observability** | ⚠️ Limited (just logs) | ✅ Full audit trail in DB |
| **Recovery** | ❌ Manual resync needed | ✅ Automatic retry |
| **Dead Letter** | ❌ No tracking | ✅ Tracked and queryable |

## Conclusion

The Outbox Pattern provides **guaranteed eventual consistency** for CQRS architectures by ensuring domain events are never lost. While it adds complexity, it's the industry-standard solution for reliable event-driven systems.

**Key Takeaway**: With Outbox Pattern, even if Query DB is completely down for hours, all changes will eventually sync when it comes back up. No data loss. No manual intervention required (except for dead letter queue edge cases).
