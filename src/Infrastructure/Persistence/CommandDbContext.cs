using Application.Common.Interfaces;
using Domain.Common;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Reflection;
using System.Text.Json;

namespace Infrastructure.Persistence;

/// <summary>
/// Command Database Context - Used for WRITE operations.
/// This is the source of truth. All writes go here.
/// Uses Outbox Pattern to ensure reliable event publishing.
/// </summary>
public class CommandDbContext : DbContext, IApplicationDbContext
{
    private readonly IMediator _mediator;
    private readonly ILogger<CommandDbContext> _logger;

    public CommandDbContext(
        DbContextOptions<CommandDbContext> options,
        IMediator mediator,
        ILogger<CommandDbContext> logger)
        : base(options)
    {
        _mediator = mediator;
        _logger = logger;
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Config> Configs => Set<Config>();
    public DbSet<Address> Addresses => Set<Address>();
    public DbSet<OutboxEvent> OutboxEvents => Set<OutboxEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply all entity configurations from this assembly
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        // Global query filter for soft deletes
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(BaseEntity).IsAssignableFrom(entityType.ClrType))
            {
                var parameter = System.Linq.Expressions.Expression.Parameter(entityType.ClrType, "e");
                var property = System.Linq.Expressions.Expression.Property(parameter, nameof(BaseEntity.IsDeleted));
                var filter = System.Linq.Expressions.Expression.Lambda(
                    System.Linq.Expressions.Expression.Equal(property, System.Linq.Expressions.Expression.Constant(false)),
                    parameter);

                modelBuilder.Entity(entityType.ClrType).HasQueryFilter(filter);
            }
        }
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Auto-update ModifiedAt timestamp
        var modifiedEntries = ChangeTracker.Entries<BaseEntity>()
            .Where(e => e.State == EntityState.Modified);

        foreach (var entry in modifiedEntries)
        {
            entry.Entity.ModifiedAt = DateTime.UtcNow;
        }

        // Get domain events before saving
        var domainEvents = ChangeTracker.Entries<BaseEntity>()
            .Select(e => e.Entity)
            .Where(e => e.DomainEvents.Any())
            .SelectMany(e => e.DomainEvents)
            .ToList();

        // OUTBOX PATTERN: Save events to outbox table BEFORE publishing
        // This ensures events are persisted and won't be lost if publishing fails
        var outboxEvents = new List<OutboxEvent>();
        foreach (var domainEvent in domainEvents)
        {
            var eventType = domainEvent.GetType().Name;
            var eventData = JsonSerializer.Serialize(domainEvent, domainEvent.GetType());

            var outboxEvent = OutboxEvent.Create(eventType, eventData);
            outboxEvents.Add(outboxEvent);
            await OutboxEvents.AddAsync(outboxEvent, cancellationToken);
        }

        // Save changes to Command DB (source of truth)
        // This includes both the business data AND the outbox events in a single transaction
        var result = await base.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("Saved {Count} changes with {EventCount} outbox events",
            result, outboxEvents.Count);

        // Try to publish events immediately (optimistic path)
        // If this fails, the background processor will retry from outbox
        foreach (var (domainEvent, outboxEvent) in domainEvents.Zip(outboxEvents))
        {
            try
            {
                await _mediator.Publish(domainEvent, cancellationToken);

                // Mark as processed in outbox
                outboxEvent.MarkAsProcessed();
                _logger.LogDebug("Successfully published {EventType} immediately",
                    domainEvent.GetType().Name);
            }
            catch (Exception ex)
            {
                // Log failure but don't throw - event is safely stored in outbox
                _logger.LogWarning(ex,
                    "Failed to publish {EventType} immediately. Will retry from outbox.",
                    domainEvent.GetType().Name);

                outboxEvent.RecordFailure(ex.Message);
            }
        }

        // Save outbox status updates (processed or failed)
        if (outboxEvents.Any())
        {
            await base.SaveChangesAsync(cancellationToken);
        }

        // Clear domain events from entities
        var entitiesWithEvents = ChangeTracker.Entries<BaseEntity>()
            .Select(e => e.Entity)
            .Where(e => e.DomainEvents.Any())
            .ToList();

        foreach (var entity in entitiesWithEvents)
        {
            entity.ClearDomainEvents();
        }

        return result;
    }
}
