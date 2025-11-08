using Domain.Common;
using Domain.Events;
using Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Infrastructure.BackgroundServices;

/// <summary>
/// Background service that processes unprocessed events from the outbox table.
/// Implements retry logic with exponential backoff for failed events.
/// </summary>
public class OutboxProcessor : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OutboxProcessor> _logger;
    private readonly TimeSpan _processingInterval = TimeSpan.FromSeconds(10);
    private const int MaxRetries = 5;

    public OutboxProcessor(
        IServiceProvider serviceProvider,
        ILogger<OutboxProcessor> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OutboxProcessor started. Will process events every {Interval} seconds.",
            _processingInterval.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessOutboxEventsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in OutboxProcessor. Will retry after interval.");
            }

            await Task.Delay(_processingInterval, stoppingToken);
        }

        _logger.LogInformation("OutboxProcessor stopped.");
    }

    private async Task ProcessOutboxEventsAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CommandDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        // Get unprocessed events that are ready for retry
        var unprocessedEvents = await dbContext.OutboxEvents
            .Where(e => e.ProcessedAt == null)
            .Where(e => e.NextRetryAt != null && e.NextRetryAt <= DateTime.UtcNow)
            .OrderBy(e => e.CreatedAt)
            .Take(100) // Process in batches
            .ToListAsync(cancellationToken);

        if (!unprocessedEvents.Any())
        {
            _logger.LogTrace("No unprocessed outbox events found.");
            return;
        }

        _logger.LogInformation("Processing {Count} unprocessed outbox events.", unprocessedEvents.Count);

        foreach (var outboxEvent in unprocessedEvents)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            // Check if max retries exceeded
            if (outboxEvent.HasExceededMaxRetries(MaxRetries))
            {
                _logger.LogError(
                    "OutboxEvent {EventId} of type {EventType} has exceeded max retries ({MaxRetries}). Moving to dead letter.",
                    outboxEvent.Id, outboxEvent.EventType, MaxRetries);

                // Mark as deleted (soft delete) - this moves it to "dead letter"
                outboxEvent.IsDeleted = true;
                continue;
            }

            try
            {
                // Deserialize the domain event
                var domainEvent = DeserializeDomainEvent(outboxEvent);

                if (domainEvent == null)
                {
                    _logger.LogError(
                        "Failed to deserialize OutboxEvent {EventId} of type {EventType}. Marking as deleted.",
                        outboxEvent.Id, outboxEvent.EventType);
                    outboxEvent.IsDeleted = true;
                    continue;
                }

                // Publish the event
                await mediator.Publish(domainEvent, cancellationToken);

                // Mark as successfully processed
                outboxEvent.MarkAsProcessed();

                _logger.LogInformation(
                    "Successfully processed OutboxEvent {EventId} of type {EventType} (attempt {Attempt}).",
                    outboxEvent.Id, outboxEvent.EventType, outboxEvent.RetryCount + 1);
            }
            catch (Exception ex)
            {
                // Record failure with exponential backoff
                outboxEvent.RecordFailure(ex.Message);

                _logger.LogWarning(ex,
                    "Failed to process OutboxEvent {EventId} of type {EventType}. Retry count: {RetryCount}. Next retry at: {NextRetry}.",
                    outboxEvent.Id, outboxEvent.EventType, outboxEvent.RetryCount, outboxEvent.NextRetryAt);
            }
        }

        // Save all changes (processed events and retry updates)
        await dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Completed processing outbox events.");
    }

    private IDomainEvent? DeserializeDomainEvent(Domain.Entities.OutboxEvent outboxEvent)
    {
        try
        {
            // Map event type name to actual type
            var eventType = outboxEvent.EventType switch
            {
                nameof(UserCreatedEvent) => typeof(UserCreatedEvent),
                nameof(UserUpdatedEvent) => typeof(UserUpdatedEvent),
                nameof(UserDeletedEvent) => typeof(UserDeletedEvent),
                nameof(ConfigCreatedEvent) => typeof(ConfigCreatedEvent),
                nameof(ConfigUpdatedEvent) => typeof(ConfigUpdatedEvent),
                nameof(ConfigDeletedEvent) => typeof(ConfigDeletedEvent),
                "AddressCreatedEvent" => typeof(Domain.Events.AddressCreatedEvent),
                "AddressUpdatedEvent" => typeof(Domain.Events.AddressUpdatedEvent),
                "AddressDeletedEvent" => typeof(Domain.Events.AddressDeletedEvent),
                _ => null
            };

            if (eventType == null)
            {
                _logger.LogError("Unknown event type: {EventType}", outboxEvent.EventType);
                return null;
            }

            return JsonSerializer.Deserialize(outboxEvent.EventData, eventType) as IDomainEvent;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deserializing event {EventType}", outboxEvent.EventType);
            return null;
        }
    }
}
