<div dir="rtl" style="font-family: IRANSans, Vazir, Tahoma, Arial, sans-serif; text-align: right;">
# نگاشت هندلر رویداد دامنه

این سند نشان می‌دهد که کدام هندلرها وقتی `_mediator.Publish(domainEvent)` فراخوانی می‌شود، صدا زده می‌شوند.

## جریان انتشار رویداد

<div dir="ltr">

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

</div>

## نگاشت رویداد دامنه → هندلر

وقتی `_mediator.Publish()` فراخوانی می‌شود، MediatR به صورت خودکار رویداد را به هندلر مربوطه مسیریابی می‌کند:

### رویدادهای کاربر

| رویداد دامنه | هندلر | مکان | عملیات |
|-------------|---------|----------|--------|
| **UserCreatedEvent** | `UserCreatedEventHandler` | `src/Application/Common/Events/UserCreatedEventHandler.cs:13` | همگام‌سازی کاربر جدید با دیتابیس Query |
| **UserUpdatedEvent** | `UserUpdatedEventHandler` | `src/Application/Common/Events/UserUpdatedEventHandler.cs:12` | به‌روزرسانی کاربر در دیتابیس Query |
| **UserDeletedEvent** | `UserDeletedEventHandler` | `src/Application/Common/Events/UserDeletedEventHandler.cs:12` | علامت‌گذاری کاربر به عنوان حذف شده در دیتابیس Query |

### رویدادهای پیکربندی

| رویداد دامنه | هندلر | مکان | عملیات |
|-------------|---------|----------|--------|
| **ConfigCreatedEvent** | `ConfigCreatedEventHandler` | `src/Application/Common/Events/ConfigCreatedEventHandler.cs:13` | همگام‌سازی پیکربندی جدید با دیتابیس Query |
| **ConfigUpdatedEvent** | `ConfigUpdatedEventHandler` | `src/Application/Common/Events/ConfigUpdatedEventHandler.cs:13` | به‌روزرسانی پیکربندی در دیتابیس Query |
| **ConfigDeletedEvent** | `ConfigDeletedEventHandler` | `src/Application/Common/Events/ConfigDeletedEventHandler.cs:13` | حذف پیکربندی از دیتابیس Query |

## چگونه MediatR رویدادها را مسیریابی می‌کند

MediatR از **Reflection و Dependency Injection** برای پیدا کردن و فراخوانی هندلرها استفاده می‌کند:

1. وقتی `_mediator.Publish(domainEvent)` را فراخوانی می‌کنید، MediatR نوع رویداد را بررسی می‌کند
2. تمام کلاس‌هایی که `INotificationHandler<TEvent>` را پیاده‌سازی کرده‌اند و `TEvent` با نوع رویداد مطابقت دارد را جستجو می‌کند
3. MediatR این هندلرها را از کانتینر DI حل می‌کند
4. متد `Handle()` را روی هر هندلر ثبت شده فراخوانی می‌کند
5. **چندین هندلر می‌توانند یک رویداد را مدیریت کنند** (الگوی اعلان)

## مثال: جریان ایجاد کاربر

<div dir="ltr">

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

</div>

## ثبت‌نام

همه هندلرهای رویداد به صورت خودکار در `src/Application/DependencyInjection.cs` ثبت می‌شوند:

<div dir="ltr">

```csharp
services.AddMediatR(config =>
{
    config.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly());
});
```

</div>

این اسمبلی Application را اسکن می‌کند و همه پیاده‌سازی‌های `INotificationHandler<>` را ثبت می‌کند.

## نکات کلیدی

1. **مسیریابی خودکار**: MediatR به صورت خودکار هندلر صحیح را بر اساس نوع رویداد پیدا می‌کند
2. **چندین هندلر**: یک رویداد می‌تواند چندین هندلر داشته باشد (در حال حاضر استفاده نمی‌شود، اما پشتیبانی می‌شود)
3. **اجرای Async**: همه هندلرها به صورت async فراخوانی می‌شوند
4. **پردازش ترتیبی**: هندلرها یکی پس از دیگری فراخوانی می‌شوند، نه به صورت موازی
5. **مدیریت خطا**: اگر هندلر استثناء پرتاب کند، به فراخواننده (CommandDbContext) حباب می‌شود
6. **تضمین همگام‌سازی**: رویدادها بعد از ذخیره موفق دیتابیس Command منتشر می‌شوند که سازگاری را تضمین می‌کند

## نکات اشکال‌زدایی

برای دیدن اینکه کدام هندلر فراخوانی می‌شود، یک نقطه توقف در این مکان‌ها اضافه کنید:
- `CommandDbContext.cs:78` - جایی که رویدادها منتشر می‌شوند
- هر متد `*EventHandler.cs:Handle()` - جایی که رویدادها پردازش می‌شوند
- متغیر `domainEvent` را بررسی کنید تا ببینید کدام رویداد در حال انتشار است

</div>
