<div dir="rtl" style="font-family: Tahoma, Arial, sans-serif; text-align: right;">

# مدیریت خطای Event Handler - مسئله طراحی بحرانی

## مشکل

وقتی `CommandDbContext.SaveChangesAsync()` رویدادهای دامنه را منتشر می‌کند، یک پنجره بحرانی برای شکست وجود دارد:


<div dir="ltr">

```csharp
// خط 72: دیتابیس Command ذخیره و commit می‌شود
var result = await base.SaveChangesAsync(cancellationToken); // ✅ COMMITTED

// خط 78: Event handler با دیتابیس Query همگام‌سازی می‌کند
await _mediator.Publish(domainEvent, cancellationToken); // ❌ ممکن است شکست بخورد!
```

</div>

**مسئله:** دیتابیس Command قبلاً commit شده است. اگر event handler شکست بخورد، شما نمی‌توانید دیتابیس Command را Rollback کنید.

## رفتار ناسازگار فعلی

### UserCreatedEventHandler - استثناها را می‌بلعد


<div dir="ltr">

```csharp
catch (Exception ex)
{
    _logger.LogError(ex, "Failed to synchronize...");
    // پرتاب نمی‌کند - استثناء بلعیده می‌شود
}
```

</div>

**نتیجه اگر handler شکست بخورد:**
- ✅ دیتابیس Command: کاربر ایجاد شد
- ❌ دیتابیس Query: کاربر ایجاد نشد
- ✅ API موفقیت به کلاینت برمی‌گرداند
- ⚠️ **ناسازگاری داده بی‌صدا!**

### ConfigCreatedEventHandler - استثناها را دوباره پرتاب می‌کند


<div dir="ltr">

```csharp
catch (Exception ex)
{
    _logger.LogError(ex, "Failed to sync...");
    throw; // دوباره پرتاب می‌کند!
}
```

</div>

**نتیجه اگر handler شکست بخورد:**
- ✅ دیتابیس Command: Config ایجاد شد (قبلاً commit شده، نمی‌تواند rollback شود!)
- ❌ دیتابیس Query: Config ایجاد نشد
- ❌ API خطا به کلاینت برمی‌گرداند
- ⚠️ **ناسازگاری داده قابل مشاهده + کاربر خطا می‌بیند حتی اگر نوشتن موفق بوده باشد!**

## چرا هر دو رویکرد مشکل‌دار هستند

| رویکرد | دیتابیس Command | دیتابیس Query | پاسخ API | مسئله |
|----------|------------|----------|--------------|-------|
| **بلعیدن استثناء** | ✅ به‌روزرسانی شد | ❌ به‌روزرسانی نشد | ✅ موفقیت | شکست بی‌صدا - داده همگام نیست |
| **پرتاب مجدد استثناء** | ✅ به‌روزرسانی شد | ❌ به‌روزرسانی نشد | ❌ خطا | گیج‌کننده - کاربر خطا می‌بیند اما نوشتن موفق بود |

## راه‌حل 1: سازگاری نهایی با تلاش مجدد (توصیه شده)

پذیرش اینکه شکست‌ها می‌توانند اتفاق بیفتند و ساخت انعطاف‌پذیری:

### پیاده‌سازی


<div dir="ltr">

```csharp
public class UserCreatedEventHandler : INotificationHandler<UserCreatedEvent>
{
    private readonly QueryDbContext _queryDb;
    private readonly ILogger<UserCreatedEventHandler> _logger;
    private readonly IEventRetryQueue _retryQueue; // وابستگی جدید

    public async Task Handle(UserCreatedEvent notification, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Syncing user to Query DB: {UserId}", notification.UserId);

            // همگام‌سازی با دیتابیس Query
            await SyncUserToQueryDb(notification, cancellationToken);

            _logger.LogInformation("Successfully synced user {UserId}", notification.UserId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to sync user {UserId}. Queueing for retry.", notification.UserId);

            // صف کردن برای تلاش مجدد به جای شکست بی‌صدا
            await _retryQueue.EnqueueAsync(notification, cancellationToken);

            // پرتاب نکنید - دیتابیس Command قبلاً commit شده
            // اجازه دهید مکانیزم تلاش مجدد سازگاری نهایی را مدیریت کند
        }
    }
}
```

</div>

### مزایا
- ✅ دیتابیس Command همیشه موفق است
- ✅ سازگاری نهایی از طریق صف تلاش مجدد
- ✅ کاربر فوراً موفقیت می‌بیند
- ✅ فرآیند پس‌زمینه همگام‌سازی‌های شکست خورده را دوباره امتحان می‌کند
- ✅ می‌توان صف تلاش مجدد را برای شکست‌ها نظارت کرد

### گزینه‌های پیاده‌سازی

**گزینه A: صف حافظه داخلی با سرویس پس‌زمینه**

<div dir="ltr">

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
            // از صف خارج کردن و تلاش مجدد رویدادهای شکست خورده
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }
    }
}
```

</div>

**گزینه B: استفاده از الگوی Outbox**

<div dir="ltr">

```csharp
// ذخیره رویدادهای شکست خورده در دیتابیس Command
public class OutboxEvent
{
    public Guid Id { get; set; }
    public string EventType { get; set; }
    public string EventData { get; set; } // JSON
    public int RetryCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
}

// سرویس پس‌زمینه outbox را پردازش می‌کند
// تلاش مجدد با backoff نمایی
// بعد از N شکست به صف dead-letter منتقل می‌شود
```

</div>

**گزینه C: استفاده از صف پیام (RabbitMQ, Azure Service Bus)**

<div dir="ltr">

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

</div>

## راه‌حل 2: Commit دو مرحله‌ای (پیچیده، توصیه نمی‌شود)

استفاده از تراکنش‌های توزیع شده در هر دو دیتابیس.

### پیاده‌سازی


<div dir="ltr">

```csharp
using var scope = new TransactionScope(
    TransactionScopeAsyncFlowOption.Enabled);

await _commandDb.SaveChangesAsync();
await _queryDb.SaveChangesAsync();

scope.Complete(); // هر دو commit یا هر دو rollback
```

</div>

### مسائل
- ❌ پیچیده و مستعد خطا
- ❌ سربار کارایی
- ❌ SQLite از تراکنش‌های توزیع شده پشتیبانی نمی‌کند
- ❌ برخلاف مدل سازگاری نهایی CQRS
- ❌ جفت شدگی محکم بین دیتابیس‌ها

## راه‌حل 3: ذخیره رویدادها در Outbox قبل از انتشار (بهترین شیوه)

ذخیره رویدادهای دامنه در دیتابیس Command قبل از انتشار.

### پیاده‌سازی


<div dir="ltr">

```csharp
public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
{
    // جمع‌آوری رویدادهای دامنه
    var domainEvents = ChangeTracker.Entries<BaseEntity>()
        .SelectMany(e => e.DomainEvents)
        .ToList();

    // ذخیره رویدادها در جدول outbox در دیتابیس Command
    foreach (var domainEvent in domainEvents)
    {
        OutboxEvents.Add(new OutboxEvent
        {
            EventType = domainEvent.GetType().Name,
            EventData = JsonSerializer.Serialize(domainEvent),
            CreatedAt = DateTime.UtcNow
        });
    }

    // ذخیره تغییرات در دیتابیس Command (شامل رویدادهای outbox)
    var result = await base.SaveChangesAsync(cancellationToken);

    // تلاش برای انتشار رویدادها
    foreach (var domainEvent in domainEvents)
    {
        try
        {
            await _mediator.Publish(domainEvent, cancellationToken);
            // علامت‌گذاری رویداد outbox به عنوان پردازش شده
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish event. Will retry from outbox.");
            // پرتاب نکنید - رویداد در outbox است، سرویس پس‌زمینه تلاش مجدد می‌کند
        }
    }

    return result;
}
```

</div>

### مزایا
- ✅ رویدادها قبل از انتشار ذخیره می‌شوند (بدون از دست رفتن رویداد)
- ✅ سرویس پس‌زمینه می‌تواند از outbox تلاش مجدد کند
- ✅ دیتابیس Command منبع حقیقت برای رویدادها است
- ✅ می‌توان همه رویدادها را ممیزی کرد
- ✅ در صورت نیاز می‌توان رویدادها را replay کرد

## راه‌حل 4: نظارت و هشدار (حداقل قابل قبول)

اگر پیاده‌سازی تلاش مجدد در ابتدا خیلی پیچیده است:


<div dir="ltr">

```csharp
catch (Exception ex)
{
    _logger.LogError(ex, "CRITICAL: Failed to sync user {UserId} to Query DB", notification.UserId);

    // ارسال هشدار به سیستم نظارت
    await _alertService.SendCriticalAlertAsync(
        "CQRS Sync Failure",
        $"User {notification.UserId} not synced to Query DB",
        ex);

    // پرتاب نکنید - اجازه دهید عملیات موفق شود
    // تیم عملیات به صورت دستی از هشدارها همگام‌سازی می‌کند
}
```

</div>

## رویکرد توصیه شده

**برای تولید:**
1. پیاده‌سازی **الگوی Outbox** (راه‌حل 3)
2. افزودن **سرویس پس‌زمینه** برای پردازش outbox با منطق تلاش مجدد
3. افزودن **صف Dead Letter** برای رویدادهایی که بعد از N تلاش شکست می‌خورند
4. افزودن **نظارت و هشدارها** برای رویدادهای شکست خورده
5. پیاده‌سازی **ابزار همگام‌سازی دستی** برای تیم عملیات

**برای توسعه/موارد ساده:**
1. بلعیدن استثناها در همه هندلرها (رفتار سازگار)
2. افزودن لاگ‌گذاری دقیق
3. افزودن نظارت/هشدار
4. مستندسازی اینکه دیتابیس Query سازگاری نهایی دارد
5. پذیرش اینکه شکست‌های نادر ممکن است نیاز به مداخله دستی داشته باشند

## توصیه‌های کد فعلی

### رفع 1: همه هندلرها را سازگار کنید (رفع سریع)

همه هندلرها باید یا:
- **استثناها را ببلعند** (پذیرش سازگاری نهایی)
- یا **استثناها را دوباره پرتاب کنند** (شکست سریع، اما برای کاربران گیج‌کننده)

من **بلعیدن + لاگ + هشدار** را برای الان توصیه می‌کنم:


<div dir="ltr">

```csharp
catch (Exception ex)
{
    _logger.LogError(ex,
        "CRITICAL: Failed to sync {Entity} {Id} to Query DB. Manual intervention may be required.",
        nameof(Config), notification.ConfigId);

    // TODO: پیاده‌سازی صف تلاش مجدد
    // برای الان، پرتاب نکنید - دیتابیس Command قبلاً commit شده
}
```

</div>

### رفع 2: افزودن نظارت

ایجاد یک چک سلامت که تعداد رکوردها را مقایسه می‌کند:


<div dir="ltr">

```csharp
public class DatabaseSyncHealthCheck : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context)
    {
        var commandUserCount = await _commandDb.Users.CountAsync();
        var queryUserCount = await _queryDb.Users.CountAsync();

        var diff = Math.Abs(commandUserCount - queryUserCount);

        if (diff > 10) // آستانه
        {
            return HealthCheckResult.Unhealthy(
                $"User count mismatch: Command={commandUserCount}, Query={queryUserCount}");
        }

        return HealthCheckResult.Healthy();
    }
}
```

</div>

## خلاصه

**مشکل اساسی:**
شما نمی‌توانید تراکنش‌های ACID را در دو دیتابیس جداگانه در این معماری داشته باشید.

**بپذیرید:**
سازگاری نهایی معاوضه CQRS با دیتابیس‌های جداگانه است.

**کاهش:**
1. بلعیدن استثناها به صورت سازگار
2. لاگ شکست‌ها به صورت برجسته
3. پیاده‌سازی مکانیزم تلاش مجدد
4. نظارت بر شکست‌های همگام‌سازی
5. ساخت ابزارهای همگام‌سازی دستی برای موارد لبه

این واقعیت سیستم‌های توزیع شده است!

---

## ✅ راه‌حل پیاده‌سازی شده: الگوی Outbox

**این پروژه اکنون الگوی Outbox را برای تضمین سازگاری نهایی پیاده‌سازی می‌کند.**

برای مستندات کامل [OUTBOX_PATTERN_IMPLEMENTATION.md](./OUTBOX_PATTERN_IMPLEMENTATION.md) را ببینید.

### خلاصه سریع

**قبل از الگوی Outbox:**

<div dir="ltr">

```csharp
await base.SaveChangesAsync();      // ✅ Committed
await _mediator.Publish(event);     // ❌ اگر شکست بخورد → رویداد از دست رفت
```

</div>

**بعد از الگوی Outbox:**

<div dir="ltr">

```csharp
// ذخیره رویداد در جدول outbox
await OutboxEvents.AddAsync(outboxEvent);
await base.SaveChangesAsync();      // ✅ داده کسب و کار + رویداد به صورت اتمی commit شدند

try {
    await _mediator.Publish(event); // تلاش برای انتشار فوری
    outboxEvent.MarkAsProcessed();  // ✅ موفقیت
} catch {
    outboxEvent.RecordFailure();    // ⚠️ از outbox تلاش مجدد خواهد شد
}
```

</div>

### چه چیزی تغییر کرد

1. **موجودیت OutboxEvent**: رویدادها را قبل از انتشار ذخیره می‌کند
2. **CommandDbContext**: رویدادها را در همان تراکنش با داده کسب و کار ذخیره می‌کند
3. **OutboxProcessor**: سرویس پس‌زمینه هر 10 ثانیه رویدادهای شکست خورده را تلاش مجدد می‌کند
4. **Backoff نمایی**: تاخیرهای تلاش مجدد 2 ثانیه، 4 ثانیه، 8 ثانیه، 16 ثانیه، 32 ثانیه
5. **صف Dead Letter**: رویدادهایی که 5+ بار شکست می‌خورند برای بررسی دستی علامت‌گذاری می‌شوند

### مزایا

- ✅ **صفر از دست رفتن رویداد**: رویدادها قبل از انتشار ذخیره می‌شوند
- ✅ **سازگاری نهایی تضمین شده**: رویدادهای شکست خورده به صورت خودکار تلاش مجدد می‌شوند
- ✅ **تخریب شایسته**: سیستم حتی اگر دیتابیس Query خاموش باشد کار می‌کند
- ✅ **رد ممیزی کامل**: همه رویدادها در دیتابیس ردیابی می‌شوند
- ✅ **قابل مشاهده**: نظارت بر اندازه صف، نرخ تلاش مجدد، dead letters

### این برای شما چه معنایی دارد

**انتشار رویداد دیگر نمی‌تواند به صورت بی‌صدا شکست بخورد.** هر رویداد دامنه:
1. در outbox ذخیره می‌شود (تضمین شده)
2. فوراً منتشر می‌شود (تلاش خوش‌بینانه)
3. اگر انتشار شکست بخورد به صورت خودکار تلاش مجدد می‌شود (موفقیت نهایی تضمین شده)
4. اگر به طور دائم شکست بخورد به صف dead letter منتقل می‌شود (مداخله دستی)

**دیتابیس Command و Query همیشه در نهایت همگام خواهند شد**، حتی از طریق:
- شکست‌های شبکه
- خرابی دیتابیس
- راه‌اندازی مجدد برنامه
- مسائل زیرساختی موقت

این راه‌حل آماده تولید برای CQRS با دیتابیس‌های جداگانه است.

</div>
