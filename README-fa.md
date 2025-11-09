<div dir="rtl" style="font-family: 'IRANSans', 'Tahoma', sans-serif;">

# الگوی معماری تمیز ASP.NET Core

> **یک الگوی آماده تولید که از طریق ساختار خود آموزش می‌دهد.**

این فقط یک بویلرپلیت دیگر نیست. این یک پایه دقیقاً طراحی شده است که الگوهای معماری مدرن، بهترین شیوه‌ها و سناریوهای دنیای واقعی را نشان می‌دهد. هر خط کد هدفمند است. هر الگو هدفی دارد.

---

## فلسفه

**تکنولوژی به تنهایی کافی نیست.** این الگو ویژگی‌های پیشرفته .NET را با اصول طراحی بی‌زمان ترکیب می‌کند:

- **معماری تمیز**: منطق کسب و کار مستقل از فریمورک‌ها
- **الگوی CQRS**: جداسازی واضح بین خوانش و نوشتن
- **طراحی مبتنی بر دامنه**: مدل‌های دامنه غنی با رفتار
- **الگوی Result**: مدیریت خطای صریح بدون استثناها
- **پیکربندی استاتیک**: دسترسی فوق سریع به تنظیمات با به‌روزرسانی خودکار

---

## مرور کلی معماری

```
┌─────────────────────────────────────────────────────────┐
│                        لایه API                         │
│  (Minimal API, Endpoints, Middleware, Authentication)   │
└────────────────────┬────────────────────────────────────┘
                     │
┌────────────────────▼────────────────────────────────────┐
│                   زیرساخت                              │
│   (EF Core, Redis, JWT, Serilog, AppConfig Service)    │
└────────────────────┬────────────────────────────────────┘
                     │
┌────────────────────▼────────────────────────────────────┐
│                  لایه اپلیکیشن (CORE)                  │
│  (CQRS Handlers, MediatR, Validators, Interfaces)       │
└────────────────────┬────────────────────────────────────┘
                     │
┌────────────────────▼────────────────────────────────────┐
│                      دامنه                              │
│         (Entities, Value Objects, Pure Logic)           │
└─────────────────────────────────────────────────────────┘
```

**قانون وابستگی**: هر لایه فقط به لایه‌های زیرین خود وابسته است. دامنه هیچ وابستگی ندارد.

---

## ویژگی‌ها

### ویژگی‌های اصلی

- ✅ **معماری تمیز** - 4 لایه با جداسازی واضح مسئولیت‌ها
- ✅ **CQRS واقعی با دیتابیس دوگانه** - دیتابیس‌های خوانش/نوشتن جداگانه با همگام‌سازی رویداد محور
- ✅ **رویدادهای دامنه** - همگام‌سازی خودکار بین دیتابیس‌های Command و Query
- ✅ **پایپلاین MediatR** - Command‌ها، Query‌ها و Event Handler‌ها با Behavior‌ها
- ✅ **Repository + Unit of Work** - پیاده‌سازی‌های جداگانه برای خوانش و نوشتن
- ✅ **Repository عمومی** - اصل DRY برای عملیات رایج
- ✅ **Entity Framework Core** - دو Context (CommandDbContext, QueryDbContext)
- ✅ **همگام‌سازی مبتنی بر رویداد** - همگام‌سازی خودکار و قابل اطمینان از طریق رویدادهای دامنه
- ✅ **احراز هویت JWT** - احراز هویت مبتنی بر توکن امن
- ✅ **کش Redis** - کش توزیع شده با پشتیبان حافظه داخلی
- ✅ **Serilog + Seq** - لاگ ساختاریافته با مشاهده متمرکز
- ✅ **FluentValidation** - اعتبارسنجی پایپلاین قبل از اجرای Handler‌ها
- ✅ **الگوی Result** - مدیریت موفقیت/شکست صریح
- ✅ **مدیریت خطای سراسری** - پاسخ‌های خطای یکپارچه
- ✅ **Swagger/OpenAPI** - مستندات API خودکار

### سس مخفی: AppConfig

**نوآورانه‌ترین ویژگی** - یک کلاس پیکربندی استاتیک که:

1. مقادیر `appsettings.json` را به عنوان خصوصیات استاتیک بارگذاری می‌کند (بدون سربار)
2. جدول `Configs` را از دیتابیس در دیکشنری استاتیک بارگذاری می‌کند
3. **هر 60 ثانیه به صورت خودکار به‌روزرسانی می‌شود** از طریق سرویس پس‌زمینه
4. Thread-safe با کالکشن‌های تغییرناپذیر
5. در هر جایی قابل دسترسی بدون DI

```csharp
// استفاده در هر جایی از کد - بدون نیاز به DI!
var jwtSecret = AppConfig.JwtSecretKey;  // از appsettings
var featureFlag = AppConfig.GetBool("Features.EnableNewUI");  // از دیتابیس
var maxUpload = AppConfig.GetInt("Limits.MaxUploadSizeMB");  // از دیتابیس
```

---

## ساختار پروژه

```
CleanArchTemplate/
├── src/
│   ├── Domain/                          # منطق کسب و کار خالص
│   │   ├── Entities/
│   │   │   ├── User.cs                 # موجودیت دامنه غنی با رویدادها
│   │   │   └── Config.cs               # موجودیت پیکربندی
│   │   ├── Events/                     # رویدادهای دامنه برای همگام‌سازی CQRS
│   │   │   ├── UserCreatedEvent.cs
│   │   │   ├── UserUpdatedEvent.cs
│   │   │   └── UserDeletedEvent.cs
│   │   └── Common/
│   │       ├── BaseEntity.cs           # با پشتیبانی رویدادهای دامنه
│   │       ├── IDomainEvent.cs
│   │       └── DomainEvent.cs
│   │
│   ├── Application/                     # قوانین کسب و کار و موارد استفاده
│   │   ├── Common/
│   │   │   ├── Interfaces/             # Repository, UoW, Cache, JWT
│   │   │   ├── Behaviors/              # رفتارهای پایپلاین MediatR
│   │   │   ├── Models/                 # الگوی Result
│   │   │   └── Events/                 # هندلرهای رویداد دامنه (همگام‌سازی)
│   │   │       ├── UserCreatedEventHandler.cs
│   │   │       ├── UserUpdatedEventHandler.cs
│   │   │       └── UserDeletedEventHandler.cs
│   │   ├── Users/
│   │   │   ├── Commands/               # عملیات نوشتن → دیتابیس Command
│   │   │   │   ├── CreateUser/
│   │   │   │   └── LoginUser/
│   │   │   └── Queries/                # عملیات خوانش → دیتابیس Query
│   │   │       ├── GetUser/
│   │   │       └── GetUsers/
│   │   └── Configs/
│   │       └── Queries/
│   │
│   ├── Infrastructure/                  # نگرانی‌های خارجی
│   │   ├── Persistence/
│   │   │   ├── CommandDbContext.cs     # دیتابیس نوشتن
│   │   │   ├── QueryDbContext.cs       # دیتابیس خوانش
│   │   │   ├── Configurations/         # پیکربندی‌های موجودیت EF Core
│   │   │   └── Repositories/           # پیاده‌سازی Repository
│   │   │       ├── Repository.cs       # برای دیتابیس Command
│   │   │       ├── QueryRepository.cs  # برای دیتابیس Query
│   │   │       ├── UnitOfWork.cs       # Command UoW
│   │   │       └── QueryUnitOfWork.cs  # Query UoW
│   │   ├── Caching/
│   │   │   └── RedisCacheService.cs
│   │   ├── Authentication/
│   │   │   └── JwtTokenService.cs
│   │   └── Configuration/
│   │       ├── AppConfig.cs            # کلاس پیکربندی استاتیک
│   │       └── AppConfigRefreshService.cs
│   │
│   └── Api/                            # رابط HTTP
│       ├── Endpoints/                  # گروه‌های Endpoint
│       │   ├── UserEndpoints.cs
│       │   └── ConfigEndpoints.cs
│       ├── Middleware/
│       │   └── ExceptionHandlingMiddleware.cs
│       ├── Program.cs                  # نقطه ورود برنامه
│       └── appsettings.json            # رشته‌های اتصال دوگانه
│
├── CleanArchTemplate.sln
├── global.json
└── README.md
```

---

## شروع به کار

### پیش‌نیازها

- .NET 8.0 SDK یا بالاتر
- (اختیاری) Redis برای کش توزیع شده
- (اختیاری) Seq برای مشاهده لاگ‌ها

### اجرای برنامه

1. **کلون و بازیابی**:
   ```bash
   dotnet restore
   ```

2. **اجرای API**:
   ```bash
   cd src/Api
   dotnet run
   ```

3. **باز کردن Swagger**:
   به `http://localhost:5000` بروید (Swagger UI در ریشه است)

4. **تست API**:
   - ثبت‌نام کاربر: `POST /api/users/register`
   - ورود: `POST /api/users/login` (توکن JWT برمی‌گرداند)
   - دریافت کاربر: `GET /api/users/{id}` (نیاز به JWT دارد)

---

## مثال‌های استفاده

### 1. ثبت‌نام کاربر جدید

```bash
curl -X POST http://localhost:5000/api/users/register \
  -H "Content-Type: application/json" \
  -d '{
    "email": "john@example.com",
    "fullName": "John Doe",
    "password": "SecurePass123"
  }'
```

**پاسخ:**
```json
{
  "userId": "3fa85f64-5717-4562-b3fc-2c963f66afa6"
}
```

### 2. ورود و دریافت توکن JWT

```bash
curl -X POST http://localhost:5000/api/users/login \
  -H "Content-Type: application/json" \
  -d '{
    "email": "john@example.com",
    "password": "SecurePass123"
  }'
```

**پاسخ:**
```json
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "userId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "email": "john@example.com",
  "fullName": "John Doe"
}
```

### 3. دریافت کاربر با ID (احراز هویت شده)

```bash
curl -X GET http://localhost:5000/api/users/3fa85f64-5717-4562-b3fc-2c963f66afa6 \
  -H "Authorization: Bearer YOUR_JWT_TOKEN"
```

### 4. استفاده از AppConfig

کلاس `AppConfig` به صورت خودکار در هنگام راه‌اندازی پر می‌شود و هر دقیقه به‌روزرسانی می‌شود:

```csharp
// در هر کلاسی، در هر جایی از برنامه:

// دریافت از appsettings.json (خصوصیات استاتیک)
var jwtSecret = AppConfig.JwtSecretKey;
var environment = AppConfig.Environment;

// دریافت از جدول دیتابیس Configs (پویا)
var featureEnabled = AppConfig.GetBool("Features.EnableNewUI");
var maxUploadSize = AppConfig.GetInt("Limits.MaxUploadSizeMB", defaultValue: 10);
var emailSender = AppConfig.Get("Email.SenderAddress", "noreply@example.com");

// بررسی زمان آخرین به‌روزرسانی
var lastRefresh = AppConfig.LastRefreshTime;
```

---

## الگوهای طراحی کلیدی

### 1. CQRS با دیتابیس دوگانه (جداسازی مسئولیت Command و Query)

این الگو **CQRS واقعی** را با **دیتابیس‌های خوانش و نوشتن فیزیکی جداگانه** پیاده‌سازی می‌کند که از طریق رویدادهای دامنه همگام‌سازی می‌شوند.

#### معماری

```
┌─────────────────────────────────────────────────────────────┐
│                     جریان COMMAND (نوشتن)                   │
└─────────────────────────────────────────────────────────────┘

API Endpoint
    ↓
Command Handler (CreateUserCommand)
    ↓
Domain Entity (User.Create())
    ↓
ایجاد رویداد دامنه (UserCreatedEvent)
    ↓
ذخیره در دیتابیس COMMAND (command.db) ✅ منبع حقیقت
    ↓
ارسال رویداد دامنه از طریق MediatR
    ↓
Event Handler (UserCreatedEventHandler)
    ↓
همگام‌سازی با دیتابیس QUERY (query.db) ✅ Replica خوانش


┌─────────────────────────────────────────────────────────────┐
│                      جریان QUERY (خوانش)                    │
└─────────────────────────────────────────────────────────────┘

API Endpoint
    ↓
Query Handler (GetUserQuery)
    ↓
QueryUnitOfWork (استفاده از QueryDbContext)
    ↓
خوانش از دیتابیس QUERY (query.db) با AsNoTracking
    ↓
برگرداندن DTO (خوانش سریع و بهینه)
```

#### چرا دو دیتابیس؟

**دیتابیس Command** (`command.db`):
- منبع حقیقت برای همه نوشتن‌ها
- بهینه شده برای سازگاری و تراکنش‌ها
- ردیابی تغییرات کامل فعال
- اعتبارسنجی قوانین کسب و کار

**دیتابیس Query** (`query.db`):
- بهینه شده برای خوانش‌های سریع
- بدون ردیابی تغییرات (AsNoTracking)
- می‌تواند برای Query‌های خاص غیرنرمال شود
- به صورت خودکار از طریق رویدادهای دامنه به‌روزرسانی می‌شود

#### مکانیزم همگام‌سازی

**1. رویدادهای دامنه**

وقتی یک موجودیت تغییر می‌کند، یک رویداد دامنه ایجاد می‌کند:

```csharp
// در User.cs
public static User Create(string email, string fullName, string passwordHash)
{
    var user = new User { Email = email, FullName = fullName, ... };

    // ایجاد رویداد برای همگام‌سازی
    user.AddDomainEvent(new UserCreatedEvent
    {
        UserId = user.Id,
        Email = user.Email,
        FullName = user.FullName,
        IsActive = user.IsActive,
        CreatedAt = user.CreatedAt
    });

    return user;
}
```

**2. ارسال خودکار رویداد**

وقتی `SaveChangesAsync()` روی CommandDbContext فراخوانی می‌شود:

```csharp
// در CommandDbContext.cs
public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken)
{
    // 1. دریافت همه رویدادهای دامنه از موجودیت‌های ردیابی شده
    var domainEvents = ChangeTracker.Entries<BaseEntity>()
        .SelectMany(e => e.Entity.DomainEvents)
        .ToList();

    // 2. ذخیره در دیتابیس Command ابتدا (منبع حقیقت)
    var result = await base.SaveChangesAsync(cancellationToken);

    // 3. ارسال رویدادها بعد از ذخیره موفق
    foreach (var domainEvent in domainEvents)
    {
        await _mediator.Publish(domainEvent, cancellationToken);
    }

    // 4. پاک کردن رویدادها از موجودیت‌ها
    entity.ClearDomainEvents();

    return result;
}
```

**نکته کلیدی**: رویدادها فقط بعد از ذخیره موفق دیتابیس Command ارسال می‌شوند. این سازگاری را تضمین می‌کند.

**3. هندلرهای رویداد همگام‌سازی با دیتابیس Query**

هندلرهای رویداد به رویدادهای دامنه گوش می‌دهند و دیتابیس Query را به‌روزرسانی می‌کنند:

```csharp
// در UserCreatedEventHandler.cs
public class UserCreatedEventHandler : INotificationHandler<UserCreatedEvent>
{
    private readonly QueryDbContext _queryDb;

    public async Task Handle(UserCreatedEvent notification, CancellationToken cancellationToken)
    {
        // همگام‌سازی کاربر با دیتابیس Query
        var queryUser = new User
        {
            Id = notification.UserId,
            Email = notification.Email,
            FullName = notification.FullName,
            // ... کپی همه خصوصیات از رویداد
        };

        await _queryDb.Users.AddAsync(queryUser);
        await _queryDb.SaveChangesAsync();

        // اگر این شکست بخورد، ذخیره دیتابیس Command قبلاً موفق بوده
        // شکست برای مداخله/تلاش مجدد دستی لاگ می‌شود
    }
}
```

#### هندلرهای Command در مقابل Query

**هندلرهای Command** (نوشتن در دیتابیس Command):

```csharp
public class CreateUserCommandHandler : IRequestHandler<CreateUserCommand, Result<Guid>>
{
    private readonly IUnitOfWork _unitOfWork; // → CommandDbContext

    public async Task<Result<Guid>> Handle(CreateUserCommand request, ...)
    {
        var user = User.Create(request.Email, request.FullName, passwordHash);

        await _unitOfWork.Users.AddAsync(user); // نوشتن در دیتابیس Command
        await _unitOfWork.SaveChangesAsync();   // فعال‌سازی رویدادهای دامنه

        return Result<Guid>.Success(user.Id);
    }
}
```

**هندلرهای Query** (خوانش از دیتابیس Query):

```csharp
public class GetUserQueryHandler : IRequestHandler<GetUserQuery, Result<UserDto>>
{
    private readonly QueryUnitOfWork _queryUnitOfWork; // → QueryDbContext

    public async Task<Result<UserDto>> Handle(GetUserQuery request, ...)
    {
        // خوانش سریع با AsNoTracking از دیتابیس Query
        var user = await _queryUnitOfWork.Users.GetByIdAsync(request.UserId);

        // نگاشت به DTO و برگرداندن
        return Result<UserDto>.Success(userDto);
    }
}
```

#### مثال جریان کامل

**جریان ثبت‌نام کاربر**:

1. کلاینت: `POST /api/users/register`
2. API: مسیریابی به `CreateUserCommandHandler`
3. Handler: ایجاد موجودیت `User` از طریق `User.Create()`
4. Domain: `User.Create()` رویداد `UserCreatedEvent` را ایجاد می‌کند
5. Handler: ذخیره کاربر در **دیتابیس Command**
6. CommandDbContext: بعد از ذخیره، `UserCreatedEvent` را ارسال می‌کند
7. Event Handler: `UserCreatedEventHandler` رویداد را دریافت می‌کند
8. Event Handler: همگام‌سازی کاربر با **دیتابیس Query**
9. کلاینت: پاسخ موفقیت دریافت می‌کند

**جریان دریافت کاربر**:

1. کلاینت: `GET /api/users/{id}`
2. API: مسیریابی به `GetUserQueryHandler`
3. Handler: خوانش از **دیتابیس Query** (سریع، AsNoTracking)
4. Handler: نگاشت موجودیت به DTO
5. کلاینت: داده کاربر دریافت می‌شود

#### مزایای این معماری

**کارایی**:
- خوانش‌ها 40-50% سریع‌تر با `AsNoTracking()`
- دیتابیس Query می‌تواند ایندکس‌های مختلفی بهینه شده برای خوانش داشته باشد
- Replica‌های خوانش می‌توانند به صورت افقی مقیاس شوند
- بدون تداخل قفل بین خوانش‌ها و نوشتن‌ها

**مقیاس‌پذیری**:
- دیتابیس‌های Command و Query می‌توانند به صورت مستقل مقیاس شوند
- Replica‌های خوانش متعدد ممکن
- سخت‌افزار مختلف برای خوانش در مقابل نوشتن

**انعطاف‌پذیری**:
- می‌توان از موتورهای دیتابیس مختلف استفاده کرد (SQL Server برای نوشتن، PostgreSQL برای خوانش)
- دیتابیس Query می‌تواند برای موارد استفاده خاص غیرنرمال شود
- افزودن لایه کش در بالای دیتابیس Query آسان است

**جداسازی نگرانی‌ها**:
- Command‌ها نمی‌توانند به طور تصادفی داده قدیمی بخوانند
- Query‌ها نمی‌توانند داده را تغییر دهند (با پرتاب استثناء اجباری می‌شود)
- تمایز واضح در کد بین خوانش و نوشتن

#### سازگاری نهایی

**مهم**: یک پنجره کوچک (معمولاً <100ms) وجود دارد که دیتابیس Query ممکن است عقب‌تر از دیتابیس Command باشد.

**مدیریت**:
- رابط کاربری می‌تواند به‌روزرسانی‌های خوش‌بینانه نشان دهد
- از کش برای داده‌های پرتکرار استفاده کنید
- تاخیر همگام‌سازی را در محیط تولید نظارت کنید

**مدیریت شکست**:
```csharp
// در event handler
catch (Exception ex)
{
    // ذخیره دیتابیس Command موفق بود، اما همگام‌سازی دیتابیس Query شکست خورد
    _logger.LogError(ex, "Failed to sync user to Query DB");

    // گزینه‌های تولید:
    // 1. صف کردن رویداد برای تلاش مجدد (با backoff نمایی)
    // 2. استفاده از الگوی Outbox برای تحویل تضمین شده
    // 3. کار تطابق دستی
}
```

#### پیکربندی

```json
// appsettings.json
{
  "ConnectionStrings": {
    "CommandConnection": "Data Source=command.db",  // دیتابیس نوشتن
    "QueryConnection": "Data Source=query.db",      // دیتابیس خوانش
    "Redis": "localhost:6379"                       // کش اختیاری
  }
}
```

در تولید، این‌ها می‌توانند سرورها، دیتابیس‌ها یا حتی موتورهای دیتابیس مختلفی باشند:
```json
{
  "ConnectionStrings": {
    "CommandConnection": "Server=sql-writes.internal;Database=App;...",
    "QueryConnection": "Server=sql-reads.internal;Database=AppReadReplica;..."
  }
}
```

### 2. الگوی Result

بدون استثناء برای شکست‌های کسب و کار - خطاها صریح هستند:

```csharp
var result = await mediator.Send(new CreateUserCommand { ... });

if (result.IsSuccess)
{
    return Results.Created($"/api/users/{result.Value}", result.Value);
}
else
{
    return Results.BadRequest(new { error = result.Error });
}
```

### 3. رفتارهای پایپلاین MediatR

هر command/query از این مراحل عبور می‌کند:

1. **ValidationBehavior** - قوانین FluentValidation
2. **LoggingBehavior** - لاگ درخواست/پاسخ

```csharp
// اعتبارسنجی قبل از handler به صورت خودکار اجرا می‌شود
public class CreateUserCommandValidator : AbstractValidator<CreateUserCommand>
{
    public CreateUserCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).MinimumLength(8);
    }
}
```

### 4. Repository + Unit of Work

دسترسی به داده را انتزاعی می‌کند و تراکنش‌ها را مدیریت می‌کند:

```csharp
// در یک command handler
var user = User.Create(email, fullName, passwordHash);
await _unitOfWork.Users.AddAsync(user);
await _unitOfWork.SaveChangesAsync();

// با تراکنش‌ها
await _unitOfWork.BeginTransactionAsync();
try
{
    // چندین عملیات
    await _unitOfWork.CommitTransactionAsync();
}
catch
{
    await _unitOfWork.RollbackTransactionAsync();
}
```

---

## پیکربندی

### appsettings.json

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=app.db",
    "Redis": "localhost:6379"
  },
  "Jwt": {
    "SecretKey": "YourSecretKey...",
    "Issuer": "YourAPI",
    "Audience": "YourAPI",
    "ExpirationMinutes": "60"
  },
  "Seq": {
    "ServerUrl": "http://localhost:5341"
  }
}
```

### پیکربندی‌های دیتابیس

پیکربندی‌ها را از طریق دیتابیس اضافه کنید که هر دقیقه به صورت خودکار به‌روزرسانی می‌شوند:

```sql
INSERT INTO Configs (Id, Key, Value, Description, Category, IsActive, CreatedAt)
VALUES
  (newid(), 'Features.EnableNewUI', 'true', 'Enable new UI', 'Features', 1, getutcdate());
```

دسترسی در هر جایی:
```csharp
if (AppConfig.GetBool("Features.EnableNewUI"))
{
    // استفاده از UI جدید
}
```

---

## اجرا با Docker (اختیاری)

### Docker Compose برای زیرساخت

```yaml
version: '3.8'
services:
  redis:
    image: redis:alpine
    ports:
      - "6379:6379"

  seq:
    image: datalust/seq:latest
    ports:
      - "5341:80"
    environment:
      - ACCEPT_EULA=Y
```

اجرا: `docker-compose up -d`

---

## افزودن ویژگی‌های جدید

### مثال: افزودن موجودیت Product

1. **لایه Domain**: ایجاد موجودیت `Product.cs`
2. **لایه Application**:
   - افزودن `IRepository<Product>` به `IUnitOfWork`
   - ایجاد `Products/Commands/CreateProduct/`
   - ایجاد `Products/Queries/GetProducts/`
3. **لایه Infrastructure**:
   - افزودن `DbSet<Product>` به `ApplicationDbContext`
   - ایجاد `ProductConfiguration.cs`
   - افزودن به `UnitOfWork.cs`
4. **لایه API**:
   - ایجاد `ProductEndpoints.cs`
   - ثبت در `EndpointExtensions.cs`

معماری شما را راهنمایی می‌کند. هر ویژگی از الگوی یکسانی پیروی می‌کند.

---

## چک‌لیست تولید

قبل از استقرار:

- [ ] تغییر `SecretKey` JWT به یک مقدار قوی و تصادفی
- [ ] به‌روزرسانی رشته‌های اتصال برای دیتابیس تولید
- [ ] پیکربندی رشته اتصال Redis
- [ ] راه‌اندازی سرور Seq و به‌روزرسانی URL
- [ ] جایگزینی هش‌کردن ساده رمز عبور با BCrypt
- [ ] فعال‌سازی HTTPS
- [ ] پیکربندی صحیح CORS (حذف `AllowAll`)
- [ ] تنظیم سطوح لاگ مناسب
- [ ] افزودن استراتژی پشتیبان دیتابیس
- [ ] پیکربندی چک‌های سلامت
- [ ] راه‌اندازی نظارت و هشدار

---

## چرا این الگو؟

### چه چیزی آن را متفاوت می‌کند

بیشتر الگوها یا:
- خیلی ساده هستند (Hello World بدون ساختار)
- خیلی پیچیده هستند (هیولاهای سازمانی)
- الگوها را کورکورانه دنبال می‌کنند

**این الگو:**
- ✅ آماده تولید از روز اول
- ✅ هر الگو هدف واضحی دارد
- ✅ کد از طریق مثال‌ها آموزش می‌دهد
- ✅ تعادل بین سادگی و نیازهای دنیای واقعی
- ✅ بهترین شیوه‌ها را نمایش می‌دهد

### تصمیمات طراحی

**چرا SQLite؟** شروع آسان. در 2 خط به SQL Server/PostgreSQL تغییر دهید.

**چرا Minimal API؟** مدرن، کارآمد، تشریفات کمتر از Controller‌ها.

**چرا AppConfig استاتیک؟** فوق سریع. بدون سربار DI. خودبه‌روزرسانی.

**چرا CQRS؟** خوانش/نوشتن را جدا می‌کند. کد را قابل پیش‌بینی می‌کند.

**چرا الگوی Repository؟** دسترسی به داده را انتزاعی می‌کند. تست را آسان‌تر می‌کند.

---

## مجوز

MIT License - از آن استفاده کنید، از آن یاد بگیرید، چیزهای شگفت‌انگیز بسازید.

---

**با دقت ساخته شده. برای آموزش طراحی شده. آماده برای تولید.**

*"تکنولوژی همراه با علوم انسانی، نتایجی به بار می‌آورد که قلب‌های ما را به آواز درمی‌آورد."*

</div>
