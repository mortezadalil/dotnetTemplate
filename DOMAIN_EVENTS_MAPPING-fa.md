<div dir="rtl" style="font-family: 'IRANSans', 'Tahoma', sans-serif;">

# نگاشت هندلر رویداد دامنه

این سند نشان می‌دهد که کدام هندلرها وقتی `_mediator.Publish(domainEvent)` فراخوانی می‌شود، صدا زده می‌شوند.

## جریان انتشار رویداد

```
CommandDbContext.SaveChangesAsync()
  │
  ├─ 1. جمع‌آوری رویدادهای دامنه از موجودیت‌های ردیابی شده
  │
  ├─ 2. ذخیره تغییرات در دیتابیس Command
  │
  ├─ 3. انتشار هر رویداد دامنه از طریق MediatR
  │    └─> await _mediator.Publish(domainEvent, cancellationToken)
  │
  └─ 4. پاک کردن رویدادهای دامنه از موجودیت‌ها
```

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

```csharp
// 1. Command Handler کاربر را ایجاد می‌کند
var user = User.Create(email, fullName, passwordHash, role);
await _unitOfWork.Users.AddAsync(user);
await _unitOfWork.SaveChangesAsync();  // CommandDbContext.SaveChangesAsync() را فعال می‌کند

// 2. User.Create() رویداد UserCreatedEvent را به مجموعه user.DomainEvents اضافه کرد

// 3. CommandDbContext.SaveChangesAsync() رویداد دامنه را تشخیص می‌دهد:
var domainEvents = ChangeTracker.Entries<BaseEntity>()
    .SelectMany(e => e.DomainEvents)  // [UserCreatedEvent] را دریافت می‌کند

// 4. در دیتابیس Command ذخیره می‌کند
await base.SaveChangesAsync();

// 5. رویداد را منتشر می‌کند
await _mediator.Publish(domainEvent);  // domainEvent همان UserCreatedEvent است

// 6. MediatR به UserCreatedEventHandler مسیریابی می‌کند
//    چون INotificationHandler<UserCreatedEvent> را پیاده‌سازی می‌کند

// 7. UserCreatedEventHandler.Handle() اجرا می‌شود:
//    - کاربر را در دیتابیس Query ایجاد می‌کند
//    - همه خصوصیات را از طریق reflection همگام‌سازی می‌کند
//    - در دیتابیس Query ذخیره می‌کند
```

## ثبت‌نام

همه هندلرهای رویداد به صورت خودکار در `src/Application/DependencyInjection.cs` ثبت می‌شوند:

```csharp
services.AddMediatR(config =>
{
    config.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly());
});
```

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
