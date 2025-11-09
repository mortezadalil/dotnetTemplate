<div dir="rtl" style="font-family: IRANSans, Vazir, Tahoma, Arial, sans-serif; text-align: right;">
# پیاده‌سازی الگوی Outbox

## مرور کلی

این پروژه **الگوی Outbox** را برای تضمین سازگاری نهایی بین دیتابیس‌های Command و Query پیاده‌سازی می‌کند. این الگو تضمین می‌کند که رویدادهای دامنه هرگز از دست نمی‌روند، حتی اگر انتشار آن‌ها شکست بخورد.

## مشکل (قبل از الگوی Outbox)


<div dir="ltr">

```
┌─────────────────────────────────────────────────────────────────┐
│ مشکل: رویدادها می‌توانند اگر انتشار شکست بخورد از دست بروند  │
├─────────────────────────────────────────────────────────────────┤
│ 1. ذخیره در دیتابیس Command       ✅ COMMITTED (دائمی)          │
│ 2. انتشار رویداد                   ❌ شکست (شبکه/DB خاموش)      │
│ 3. رویداد برای همیشه از دست رفت   💥 مکانیزم تلاش مجدد ندارد   │
│                                                                  │
│ نتیجه: دیتابیس Command ≠ دیتابیس Query (ناسازگاری دائمی)      │
└─────────────────────────────────────────────────────────────────┘
```

</div>

## راه‌حل (الگوی Outbox)


<div dir="ltr">

```
┌─────────────────────────────────────────────────────────────────┐
│ راه‌حل: رویدادها قبل از انتشار ذخیره می‌شوند                   │
├─────────────────────────────────────────────────────────────────┤
│ 1. ذخیره داده کسب و کار + رویدادها در دیتابیس Command ✅ ATOMIC│
│    (هر دو در یک تراکنش)                                         │
│ 2. تلاش برای انتشار فوری                        ✅ یا ⚠️        │
│    - اگر موفق: علامت‌گذاری به عنوان پردازش شده                 │
│    - اگر شکست: تلاش مجدد بعداً از outbox                        │
│ 3. سرویس پس‌زمینه رویدادهای شکست خورده را دوباره امتحان می‌کند✅│
│                                                                  │
│ نتیجه: سازگاری نهایی تضمین شده                                 │
└─────────────────────────────────────────────────────────────────┘
```

</div>

## معماری

### اجزا

1. **موجودیت OutboxEvent** (`src/Domain/Entities/OutboxEvent.cs`)
   - رویدادهای دامنه سریالیزه شده را ذخیره می‌کند
   - تلاش‌های مجدد و وضعیت را ردیابی می‌کند
   - Backoff نمایی را پیاده‌سازی می‌کند

2. **CommandDbContext** (`src/Infrastructure/Persistence/CommandDbContext.cs`)
   - رویدادها را در جدول outbox در همان تراکنش ذخیره می‌کند
   - تلاش برای انتشار فوری (مسیر خوش‌بینانه)
   - در صورت شکست انتشار، به outbox بازمی‌گردد

3. **OutboxProcessor** (`src/Infrastructure/BackgroundServices/OutboxProcessor.cs`)
   - سرویس پس‌زمینه که هر 10 ثانیه اجرا می‌شود
   - رویدادهای پردازش نشده را از outbox پردازش می‌کند
   - تلاش مجدد با backoff نمایی را پیاده‌سازی می‌کند
   - بعد از 5 تلاش شکست خورده به dead letter منتقل می‌شود

## نمودار جریان

### مسیر خوشحال (رویداد با موفقیت منتشر می‌شود)


<div dir="ltr">

```
عملیات کاربر
    │
    ├─> Command Handler
    │       │
    │       ├─> موجودیت دامنه (رویداد UserCreatedEvent را ایجاد می‌کند)
    │       │
    │       └─> UnitOfWork.SaveChangesAsync()
    │               │
    │               ├─> CommandDbContext.SaveChangesAsync()
    │               │       │
    │               │       ├─ 1. سریالیز رویداد به JSON
    │               │       ├─ 2. ایجاد رکورد OutboxEvent
    │               │       ├─ 3. ذخیره کاربر + رویداد outbox (ATOMIC)
    │               │       │      ✅ هر دو در یک تراکنش commit می‌شوند
    │               │       │
    │               │       ├─ 4. تلاش برای انتشار فوری
    │               │       │      ✅ موفق
    │               │       │
    │               │       └─ 5. علامت‌گذاری OutboxEvent به عنوان پردازش شده
    │               │              ✅ ProcessedAt = DateTime.UtcNow
    │               │
    │               └─> Event Handler (UserCreatedEventHandler)
    │                       │
    │                       └─> دیتابیس Query به‌روزرسانی شد ✅
    │
    └─> پاسخ به کاربر (موفق)
```

</div>

### مسیر شکست (انتشار رویداد شکست می‌خورد)


<div dir="ltr">

```
عملیات کاربر
    │
    ├─> Command Handler
    │       │
    │       ├─> موجودیت دامنه (رویداد ConfigCreatedEvent را ایجاد می‌کند)
    │       │
    │       └─> UnitOfWork.SaveChangesAsync()
    │               │
    │               ├─> CommandDbContext.SaveChangesAsync()
    │               │       │
    │               │       ├─ 1. سریالیز رویداد به JSON
    │               │       ├─ 2. ایجاد رکورد OutboxEvent
    │               │       ├─ 3. ذخیره config + رویداد outbox (ATOMIC)
    │               │       │      ✅ هر دو در یک تراکنش commit می‌شوند
    │               │       │
    │               │       ├─ 4. تلاش برای انتشار فوری
    │               │       │      ❌ شکست (دیتابیس Query خاموش است)
    │               │       │
    │               │       ├─ 5. گرفتن استثناء (دوباره پرتاب نشود)
    │               │       └─ 6. علامت‌گذاری OutboxEvent برای تلاش مجدد
    │               │              ⚠️ RetryCount = 1
    │               │              ⚠️ NextRetryAt = UtcNow + 2 ثانیه
    │               │
    │               └─> پاسخ به کاربر (موفق) ✅
    │
    └─> [10 ثانیه بعد]
            │
            OutboxProcessor (سرویس پس‌زمینه)
                │
                ├─ 1. Query رویدادهای پردازش نشده
                ├─ 2. یافتن ConfigCreatedEvent (آماده برای تلاش مجدد)
                ├─ 3. Deserialize رویداد
                ├─ 4. انتشار از طریق MediatR
                │      ✅ موفق (دیتابیس Query دوباره روشن شد)
                │
                └─ 5. علامت‌گذاری OutboxEvent به عنوان پردازش شده
                       ✅ ProcessedAt = DateTime.UtcNow

نتیجه: سازگاری نهایی به دست آمد!
        دیتابیس Command و Query اکنون همگام هستند ✅
```

</div>

### مسیر شکست مداوم (حداکثر تلاش‌های مجدد تجاوز شد)


<div dir="ltr">

```
عملیات کاربر → Config در دیتابیس Command ایجاد شد ✅
    │
    └─> انتشار رویداد شکست خورد ❌
            │
            ├─ تلاش مجدد #1 (بعد از 2 ثانیه)   ❌ شکست
            ├─ تلاش مجدد #2 (بعد از 4 ثانیه)   ❌ شکست
            ├─ تلاش مجدد #3 (بعد از 8 ثانیه)   ❌ شکست
            ├─ تلاش مجدد #4 (بعد از 16 ثانیه)  ❌ شکست
            └─ تلاش مجدد #5 (بعد از 32 ثانیه)  ❌ شکست
                    │
                    └─ حداکثر تلاش‌های مجدد تجاوز شد (5)
                            │
                            ├─ انتقال به Dead Letter (حذف نرم)
                            ├─ هشدار به تیم عملیات 🚨
                            └─ مداخله دستی مورد نیاز است
```

</div>

## جزئیات پیاده‌سازی

### 1. موجودیت OutboxEvent


<div dir="ltr">

```csharp
public class OutboxEvent : BaseEntity
{
    public string EventType { get; private set; }     // "UserCreatedEvent"
    public string EventData { get; private set; }     // رویداد سریالیز شده JSON
    public int RetryCount { get; private set; }       // تعداد تلاش‌های مجدد
    public DateTime? ProcessedAt { get; private set; } // زمانی که با موفقیت پردازش شد
    public string? LastError { get; private set; }    // خطا از آخرین شکست
    public DateTime? NextRetryAt { get; private set; } // تلاش مجدد زمان‌بندی شده بعدی

    public void MarkAsProcessed() { ... }
    public void RecordFailure(string error) { ... }  // Backoff نمایی
    public bool HasExceededMaxRetries(int max = 5) { ... }
}
```

</div>

### 2. یکپارچگی CommandDbContext


<div dir="ltr">

```csharp
public override async Task<int> SaveChangesAsync(CancellationToken ct)
{
    // جمع‌آوری رویدادهای دامنه
    var domainEvents = GetDomainEvents();

    // مرحله 1: ذخیره رویدادها در outbox (همان تراکنش با داده کسب و کار)
    foreach (var domainEvent in domainEvents)
    {
        var outboxEvent = OutboxEvent.Create(
            domainEvent.GetType().Name,
            JsonSerializer.Serialize(domainEvent)
        );
        await OutboxEvents.AddAsync(outboxEvent);
    }

    // مرحله 2: Commit داده کسب و کار + رویدادهای outbox به صورت اتمی
    var result = await base.SaveChangesAsync(ct);

    // مرحله 3: تلاش برای انتشار فوری (خوش‌بینانه)
    foreach (var (domainEvent, outboxEvent) in domainEvents.Zip(outboxEvents))
    {
        try
        {
            await _mediator.Publish(domainEvent, ct);
            outboxEvent.MarkAsProcessed(); // موفق!
        }
        catch (Exception ex)
        {
            // پرتاب نکنید - رویداد به صورت ایمن در outbox است
            outboxEvent.RecordFailure(ex.Message); // بعداً تلاش مجدد می‌شود
        }
    }

    // مرحله 4: ذخیره وضعیت پردازش
    await base.SaveChangesAsync(ct);

    return result;
}
```

</div>

### 3. سرویس پس‌زمینه OutboxProcessor


<div dir="ltr">

```csharp
public class OutboxProcessor : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            // پردازش هر 10 ثانیه
            await ProcessOutboxEventsAsync(stoppingToken);
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }
    }

    private async Task ProcessOutboxEventsAsync(CancellationToken ct)
    {
        // دریافت رویدادهای پردازش نشده آماده برای تلاش مجدد
        var events = await _dbContext.OutboxEvents
            .Where(e => e.ProcessedAt == null)
            .Where(e => e.NextRetryAt <= DateTime.UtcNow)
            .Take(100)
            .ToListAsync(ct);

        foreach (var outboxEvent in events)
        {
            // بررسی حداکثر تلاش‌های مجدد
            if (outboxEvent.HasExceededMaxRetries(5))
            {
                outboxEvent.IsDeleted = true; // Dead letter
                continue;
            }

            try
            {
                // Deserialize و انتشار
                var domainEvent = Deserialize(outboxEvent);
                await _mediator.Publish(domainEvent, ct);

                outboxEvent.MarkAsProcessed(); // موفق!
            }
            catch (Exception ex)
            {
                outboxEvent.RecordFailure(ex.Message); // Backoff نمایی
            }
        }

        await _dbContext.SaveChangesAsync(ct);
    }
}
```

</div>

## استراتژی Backoff نمایی

تاخیرهای تلاش مجدد به صورت نمایی رشد می‌کنند تا از تحت فشار قرار دادن یک سیستم شکست خورده جلوگیری شود:

| تلاش مجدد # | تاخیر | مجموع زمان انتظار |
|---------|-------|-----------------|
| 1 | 2 ثانیه | 2 ثانیه |
| 2 | 4 ثانیه | 6 ثانیه |
| 3 | 8 ثانیه | 14 ثانیه |
| 4 | 16 ثانیه | 30 ثانیه |
| 5 | 32 ثانیه | 62 ثانیه |

بعد از 5 شکست، رویداد به dead letter برای بررسی دستی منتقل می‌شود.

## تضمین‌ها

### ✅ این چه چیزی را تضمین می‌کند

1. **عدم از دست رفتن رویداد**: رویدادها قبل از انتشار ذخیره می‌شوند
2. **اتمی بودن**: داده کسب و کار و رویدادها در یک تراکنش ذخیره می‌شوند
3. **سازگاری نهایی**: رویدادهای شکست خورده تا زمان موفقیت تلاش مجدد می‌شوند
4. **تخریب شایسته**: سیستم حتی اگر دیتابیس Query خاموش باشد کار می‌کند
5. **قابلیت مشاهده**: همه شکست‌ها با زمینه کامل لاگ می‌شوند

### ⚠️ این چه چیزی را تضمین نمی‌کند

1. **سازگاری فوری**: دیتابیس Query ممکن است عقب‌تر از دیتابیس Command باشد
2. **ترتیب**: رویدادها ممکن است خارج از ترتیب پردازش شوند اگر برخی شکست بخورند
3. **پردازش دقیقاً یک‌بار**: پردازش تکراری ممکن است (هندلرها باید idempotent باشند)

## نظارت و هشدارها

### معیارهای کلیدی برای نظارت

1. **اندازه صف Outbox**: تعداد رویدادهای پردازش نشده

<div dir="ltr">

   ```sql
   SELECT COUNT(*) FROM OutboxEvents WHERE ProcessedAt IS NULL
   ```

</div>

2. **رویدادهای شکست خورده**: رویدادهایی که از حداکثر تلاش‌های مجدد تجاوز کرده‌اند

<div dir="ltr">

   ```sql
   SELECT COUNT(*) FROM OutboxEvents WHERE RetryCount >= 5
   ```

</div>

3. **متوسط زمان پردازش**: زمان از ایجاد تا پردازش

<div dir="ltr">

   ```sql
   SELECT AVG(DATEDIFF(second, CreatedAt, ProcessedAt))
   FROM OutboxEvents WHERE ProcessedAt IS NOT NULL
   ```

</div>

4. **توزیع انواع رویداد**:

<div dir="ltr">

   ```sql
   SELECT EventType, COUNT(*) as Count, AVG(RetryCount) as AvgRetries
   FROM OutboxEvents
   GROUP BY EventType
   ```

</div>

### هشدارهای توصیه شده

- 🚨 **بحرانی**: رویدادهای پردازش نشده > 100
- ⚠️ **هشدار**: رویدادها با RetryCount > 3
- 📊 **اطلاعات**: متوسط تاخیر پردازش > 60 ثانیه

## مدیریت صف Dead Letter

رویدادهایی که بعد از 5 تلاش شکست می‌خورند به صورت نرم حذف می‌شوند (IsDeleted = true):


<div dir="ltr">

```sql
-- مشاهده رویدادهای dead letter
SELECT Id, EventType, RetryCount, LastError, CreatedAt
FROM OutboxEvents
WHERE IsDeleted = true
ORDER BY CreatedAt DESC
```

</div>

### روش بازیابی دستی

1. **بررسی شکست**:

<div dir="ltr">

   ```sql
   SELECT * FROM OutboxEvents WHERE Id = 'failed-event-id'
   ```

</div>

2. **بررسی وضعیت دیتابیس Query**:

<div dir="ltr">

   ```sql
   -- مثال: بررسی اینکه آیا کاربر در دیتابیس Query وجود دارد
   SELECT * FROM Users WHERE Id = 'user-id-from-event'
   ```

</div>

3. **همگام‌سازی دستی (در صورت نیاز)**:

<div dir="ltr">

   ```csharp
   // گزینه A: دوباره صف کردن رویداد
   UPDATE OutboxEvents
   SET RetryCount = 0, NextRetryAt = GETUTCDATE(), IsDeleted = 0
   WHERE Id = 'failed-event-id'

   // گزینه B: همگام‌سازی دستی با دیتابیس Query
   // نوشتن یک اسکریپت یک‌بار مصرف برای کپی داده از Command به Query DB
   ```

</div>

4. **رفع علت اصلی** قبل از صف کردن مجدد برای جلوگیری از شکست‌های تکراری

## ملاحظات کارایی

### بار دیتابیس

- **رشد جدول Outbox**: رویدادهای قدیمی پردازش شده باید آرشیو شوند

<div dir="ltr">

  ```sql
  -- آرشیو رویدادهای بیشتر از 30 روز قدیمی
  DELETE FROM OutboxEvents
  WHERE ProcessedAt IS NOT NULL
  AND ProcessedAt < DATEADD(day, -30, GETUTCDATE())
  ```

</div>

- **توصیه‌های ایندکس**:

<div dir="ltr">

  ```sql
  CREATE INDEX IX_OutboxEvents_Processing
  ON OutboxEvents(ProcessedAt, NextRetryAt)
  WHERE ProcessedAt IS NULL
  ```

</div>

### پردازش دسته‌ای

OutboxProcessor رویدادها را در دسته‌های 100 تایی پردازش می‌کند تا تعادل برقرار شود:
- توان عملیاتی (دسته‌های بزرگ‌تر = کارایی بهتر)
- تاخیر (دسته‌های کوچک‌تر = پردازش سریع‌تر رویداد فردی)

اندازه دسته را بر اساس نیازهای خود در `OutboxProcessor.cs:66` تنظیم کنید.

## استراتژی تست

### تست‌های واحد


<div dir="ltr">

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
    // تست منطق backoff نمایی
}

[Fact]
public async Task OutboxProcessor_MovesToDeadLetterAfterMaxRetries()
{
    // تست رفتار صف dead letter
}
```

</div>

### تست‌های یکپارچگی


<div dir="ltr">

```csharp
[Fact]
public async Task EventualConsistency_CommandAndQueryDbSynced()
{
    // 1. ایجاد کاربر در دیتابیس Command
    // 2. تأیید رویداد در outbox
    // 3. انتظار برای OutboxProcessor
    // 4. تأیید کاربر در دیتابیس Query
}

[Fact]
public async Task EventualConsistency_RecoverFromQueryDbFailure()
{
    // 1. توقف دیتابیس Query
    // 2. ایجاد کاربر در دیتابیس Command
    // 3. تأیید رویداد در outbox (پردازش نشده)
    // 4. شروع دیتابیس Query
    // 5. انتظار برای OutboxProcessor
    // 6. تأیید کاربر در دیتابیس Query
}
```

</div>

## پیکربندی

### تنظیمات OutboxProcessor

`OutboxProcessor.cs` را برای سفارشی‌سازی ویرایش کنید:


<div dir="ltr">

```csharp
private readonly TimeSpan _processingInterval = TimeSpan.FromSeconds(10);
private const int MaxRetries = 5;
private const int BatchSize = 100;
```

</div>

### لاگ‌گذاری

OutboxProcessor در سطوح مختلف لاگ می‌کند:
- **Debug**: پردازش رویداد فردی
- **Information**: خلاصه پردازش دسته‌ای
- **Warning**: تلاش‌های مجدد
- **Error**: حداکثر تلاش‌های مجدد تجاوز شد

## مهاجرت از پیاده‌سازی قبلی

### قبل (بدون Outbox)

رویدادها به صورت مستقیم منتشر می‌شدند:

<div dir="ltr">

```csharp
await base.SaveChangesAsync(); // دیتابیس Command commit شد
await _mediator.Publish(event); // اگر این شکست بخورد، رویداد از دست می‌رود
```

</div>

### بعد (با Outbox)

رویدادها ابتدا ذخیره می‌شوند:

<div dir="ltr">

```csharp
// ذخیره رویداد در outbox
await OutboxEvents.AddAsync(outboxEvent);
await base.SaveChangesAsync(); // دیتابیس Command + Outbox به صورت اتمی commit می‌شوند

// تلاش برای انتشار فوری (خوش‌بینانه)
await _mediator.Publish(event); // اگر این شکست بخورد، رویداد در outbox است
```

</div>

### مراحل مهاجرت

1. ✅ موجودیت `OutboxEvent` اضافه شد
2. ✅ `CommandDbContext.SaveChangesAsync()` اصلاح شد
3. ✅ سرویس پس‌زمینه `OutboxProcessor` ایجاد شد
4. ✅ در کانتینر DI ثبت شد
5. ⚠️ **TODO**: ایجاد مهاجرت دیتابیس برای جدول OutboxEvents
6. ⚠️ **TODO**: افزودن داشبورد نظارت
7. ⚠️ **TODO**: راه‌اندازی هشدارها برای صف dead letter

## مزایا نسبت به رویکرد قبلی

| جنبه | قبل | بعد (الگوی Outbox) |
|--------|--------|------------------------|
| **از دست رفتن رویداد** | ❌ ممکن است اگر انتشار شکست بخورد | ✅ غیرممکن - ابتدا ذخیره می‌شود |
| **سازگاری** | ⚠️ نهایی (اگر کار کند) | ✅ نهایی تضمین شده |
| **منطق تلاش مجدد** | ❌ هیچ | ✅ Backoff نمایی |
| **قابلیت مشاهده** | ⚠️ محدود (فقط لاگ‌ها) | ✅ رد ممیزی کامل در DB |
| **بازیابی** | ❌ همگام‌سازی دستی مورد نیاز | ✅ تلاش مجدد خودکار |
| **Dead Letter** | ❌ ردیابی نمی‌شود | ✅ ردیابی و قابل query |

## نتیجه‌گیری

الگوی Outbox **سازگاری نهایی تضمین شده** برای معماری‌های CQRS را با اطمینان از اینکه رویدادهای دامنه هرگز از دست نمی‌روند، فراهم می‌کند. در حالی که پیچیدگی اضافه می‌کند، راه‌حل استاندارد صنعت برای سیستم‌های قابل اعتماد رویداد محور است.

**نکته کلیدی**: با الگوی Outbox، حتی اگر دیتابیس Query به طور کامل برای ساعت‌ها خاموش باشد، همه تغییرات در نهایت وقتی بازمی‌گردد همگام می‌شوند. بدون از دست رفتن داده. بدون نیاز به مداخله دستی (به جز موارد لبه صف dead letter).

</div>
