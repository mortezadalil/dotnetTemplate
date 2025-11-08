# Domain Event Handler Mapping

This document shows which handlers are called when `_mediator.Publish(domainEvent)` is invoked.

## Event Publishing Flow

```
CommandDbContext.SaveChangesAsync()
  │
  ├─ 1. Collect domain events from tracked entities
  │
  ├─ 2. Save changes to Command DB
  │
  ├─ 3. Publish each domain event via MediatR
  │    └─> await _mediator.Publish(domainEvent, cancellationToken)
  │
  └─ 4. Clear domain events from entities
```

## Domain Event → Handler Mapping

When `_mediator.Publish()` is called, MediatR automatically routes the event to the corresponding handler(s):

### User Events

| Domain Event | Handler | Location | Action |
|-------------|---------|----------|--------|
| **UserCreatedEvent** | `UserCreatedEventHandler` | `src/Application/Common/Events/UserCreatedEventHandler.cs:13` | Syncs new user to Query DB |
| **UserUpdatedEvent** | `UserUpdatedEventHandler` | `src/Application/Common/Events/UserUpdatedEventHandler.cs:12` | Updates user in Query DB |
| **UserDeletedEvent** | `UserDeletedEventHandler` | `src/Application/Common/Events/UserDeletedEventHandler.cs:12` | Marks user as deleted in Query DB |

### Config Events

| Domain Event | Handler | Location | Action |
|-------------|---------|----------|--------|
| **ConfigCreatedEvent** | `ConfigCreatedEventHandler` | `src/Application/Common/Events/ConfigCreatedEventHandler.cs:13` | Syncs new config to Query DB |
| **ConfigUpdatedEvent** | `ConfigUpdatedEventHandler` | `src/Application/Common/Events/ConfigUpdatedEventHandler.cs:13` | Updates config in Query DB |
| **ConfigDeletedEvent** | `ConfigDeletedEventHandler` | `src/Application/Common/Events/ConfigDeletedEventHandler.cs:13` | Removes config from Query DB |

## How MediatR Routes Events

MediatR uses **reflection and dependency injection** to find and invoke handlers:

1. When you call `_mediator.Publish(domainEvent)`, MediatR inspects the event type
2. It looks for all classes implementing `INotificationHandler<TEvent>` where `TEvent` matches the event type
3. MediatR resolves these handlers from the DI container
4. It calls the `Handle()` method on each registered handler
5. **Multiple handlers can handle the same event** (notification pattern)

## Example: User Creation Flow

```csharp
// 1. Command Handler creates user
var user = User.Create(email, fullName, passwordHash, role);
await _unitOfWork.Users.AddAsync(user);
await _unitOfWork.SaveChangesAsync();  // Triggers CommandDbContext.SaveChangesAsync()

// 2. User.Create() added UserCreatedEvent to user.DomainEvents collection

// 3. CommandDbContext.SaveChangesAsync() detects the domain event:
var domainEvents = ChangeTracker.Entries<BaseEntity>()
    .SelectMany(e => e.DomainEvents)  // Gets [UserCreatedEvent]

// 4. Saves to Command DB
await base.SaveChangesAsync();

// 5. Publishes the event
await _mediator.Publish(domainEvent);  // domainEvent is UserCreatedEvent

// 6. MediatR routes to UserCreatedEventHandler
//    because it implements INotificationHandler<UserCreatedEvent>

// 7. UserCreatedEventHandler.Handle() executes:
//    - Creates user in Query DB
//    - Syncs all properties via reflection
//    - Saves to Query DB
```

## Registration

All event handlers are automatically registered in `src/Application/DependencyInjection.cs`:

```csharp
services.AddMediatR(config =>
{
    config.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly());
});
```

This scans the Application assembly and registers all `INotificationHandler<>` implementations.

## Key Points

1. **Automatic Routing**: MediatR automatically finds the correct handler based on event type
2. **Multiple Handlers**: One event can have multiple handlers (not currently used, but supported)
3. **Async Execution**: All handlers are called asynchronously
4. **Sequential Processing**: Handlers are called one after another, not in parallel
5. **Error Handling**: If a handler throws an exception, it will bubble up to the caller (CommandDbContext)
6. **Sync Guarantee**: Events are published AFTER the Command DB save succeeds, ensuring consistency

## Debugging Tips

To see which handler is called, add a breakpoint in:
- `CommandDbContext.cs:78` - Where events are published
- Any `*EventHandler.cs:Handle()` method - Where events are processed
- Check the `domainEvent` variable type to see which event is being published
