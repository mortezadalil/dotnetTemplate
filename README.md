# ASP.NET Core Clean Architecture Template

> **A production-ready template that teaches through its structure.**

This isn't just another boilerplate. It's a carefully crafted foundation that demonstrates modern architectural patterns, best practices, and real-world scenarios. Every line of code is intentional. Every pattern has a purpose.

---

## Philosophy

**Technology alone is not enough.** This template combines cutting-edge .NET features with timeless design principles:

- **Clean Architecture**: Business logic independent of frameworks
- **CQRS Pattern**: Clear separation between reads and writes
- **Domain-Driven Design**: Rich domain models with behavior
- **Result Pattern**: Explicit error handling without exceptions
- **Static Configuration**: Blazing-fast config access with auto-refresh

---

## Architecture Overview

```
┌─────────────────────────────────────────────────────────┐
│                        API Layer                        │
│  (Minimal API, Endpoints, Middleware, Authentication)   │
└────────────────────┬────────────────────────────────────┘
                     │
┌────────────────────▼────────────────────────────────────┐
│                   Infrastructure                        │
│   (EF Core, Redis, JWT, Serilog, AppConfig Service)    │
└────────────────────┬────────────────────────────────────┘
                     │
┌────────────────────▼────────────────────────────────────┐
│                  Application (CORE)                     │
│  (CQRS Handlers, MediatR, Validators, Interfaces)       │
└────────────────────┬────────────────────────────────────┘
                     │
┌────────────────────▼────────────────────────────────────┐
│                      Domain                             │
│         (Entities, Value Objects, Pure Logic)           │
└─────────────────────────────────────────────────────────┘
```

**Dependency Rule**: Each layer depends only on layers below it. Domain has zero dependencies.

---

## Features

### Core Features

- ✅ **Clean Architecture** - 4 layers with clear separation of concerns
- ✅ **CQRS with MediatR** - Commands and Queries with pipeline behaviors
- ✅ **Repository + Unit of Work** - Abstracted data access
- ✅ **Generic Repository** - DRY principle for common operations
- ✅ **Entity Framework Core** - SQLite (easily switch to SQL Server/PostgreSQL)
- ✅ **JWT Authentication** - Secure token-based auth
- ✅ **Redis Caching** - Distributed caching with fallback to in-memory
- ✅ **Serilog + Seq** - Structured logging with centralized viewing
- ✅ **FluentValidation** - Pipeline validation before handlers execute
- ✅ **Result Pattern** - Explicit success/failure handling
- ✅ **Global Exception Handling** - Consistent error responses
- ✅ **Swagger/OpenAPI** - Auto-generated API documentation

### The Secret Sauce: AppConfig

**The most innovative feature** - A static configuration class that:

1. Loads `appsettings.json` values as static properties (zero overhead)
2. Loads `Configs` table from database into static dictionary
3. **Auto-refreshes every 60 seconds** via background service
4. Thread-safe with immutable collections
5. Accessible anywhere without DI

```csharp
// Usage anywhere in your code - no DI required!
var jwtSecret = AppConfig.JwtSecretKey;  // From appsettings
var featureFlag = AppConfig.GetBool("Features.EnableNewUI");  // From database
var maxUpload = AppConfig.GetInt("Limits.MaxUploadSizeMB");  // From database
```

---

## Project Structure

```
CleanArchTemplate/
├── src/
│   ├── Domain/                          # Pure business logic
│   │   ├── Entities/
│   │   │   ├── User.cs                 # Rich domain entity
│   │   │   └── Config.cs               # Configuration entity
│   │   └── Common/
│   │       └── BaseEntity.cs           # Base for all entities
│   │
│   ├── Application/                     # Business rules & use cases
│   │   ├── Common/
│   │   │   ├── Interfaces/             # Repository, UoW, Cache, JWT
│   │   │   ├── Behaviors/              # MediatR pipeline behaviors
│   │   │   └── Models/                 # Result pattern
│   │   ├── Users/
│   │   │   ├── Commands/               # Write operations
│   │   │   │   ├── CreateUser/
│   │   │   │   └── LoginUser/
│   │   │   └── Queries/                # Read operations
│   │   │       ├── GetUser/
│   │   │       └── GetUsers/
│   │   └── Configs/
│   │       └── Queries/
│   │
│   ├── Infrastructure/                  # External concerns
│   │   ├── Persistence/
│   │   │   ├── ApplicationDbContext.cs
│   │   │   ├── Configurations/         # EF Core entity configs
│   │   │   └── Repositories/           # Repository implementations
│   │   ├── Caching/
│   │   │   └── RedisCacheService.cs
│   │   ├── Authentication/
│   │   │   └── JwtTokenService.cs
│   │   └── Configuration/
│   │       ├── AppConfig.cs            # Static config class
│   │       └── AppConfigRefreshService.cs
│   │
│   └── Api/                            # HTTP interface
│       ├── Endpoints/                  # Endpoint groups
│       │   ├── UserEndpoints.cs
│       │   └── ConfigEndpoints.cs
│       ├── Middleware/
│       │   └── ExceptionHandlingMiddleware.cs
│       ├── Program.cs                  # Application entry point
│       └── appsettings.json
│
├── CleanArchTemplate.sln
├── global.json
└── README.md
```

---

## Getting Started

### Prerequisites

- .NET 8.0 SDK or later
- (Optional) Redis for distributed caching
- (Optional) Seq for log viewing

### Running the Application

1. **Clone and restore**:
   ```bash
   dotnet restore
   ```

2. **Run the API**:
   ```bash
   cd src/Api
   dotnet run
   ```

3. **Open Swagger**:
   Navigate to `http://localhost:5000` (Swagger UI is at root)

4. **Test the API**:
   - Register a user: `POST /api/users/register`
   - Login: `POST /api/users/login` (returns JWT token)
   - Get user: `GET /api/users/{id}` (requires JWT)

---

## Usage Examples

### 1. Register a New User

```bash
curl -X POST http://localhost:5000/api/users/register \
  -H "Content-Type: application/json" \
  -d '{
    "email": "john@example.com",
    "fullName": "John Doe",
    "password": "SecurePass123"
  }'
```

**Response:**
```json
{
  "userId": "3fa85f64-5717-4562-b3fc-2c963f66afa6"
}
```

### 2. Login and Get JWT Token

```bash
curl -X POST http://localhost:5000/api/users/login \
  -H "Content-Type: application/json" \
  -d '{
    "email": "john@example.com",
    "password": "SecurePass123"
  }'
```

**Response:**
```json
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "userId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "email": "john@example.com",
  "fullName": "John Doe"
}
```

### 3. Get User by ID (Authenticated)

```bash
curl -X GET http://localhost:5000/api/users/3fa85f64-5717-4562-b3fc-2c963f66afa6 \
  -H "Authorization: Bearer YOUR_JWT_TOKEN"
```

### 4. Using AppConfig

The `AppConfig` class is automatically populated on startup and refreshes every minute:

```csharp
// In any class, anywhere in your application:

// Get from appsettings.json (static properties)
var jwtSecret = AppConfig.JwtSecretKey;
var environment = AppConfig.Environment;

// Get from Configs database table (dynamic)
var featureEnabled = AppConfig.GetBool("Features.EnableNewUI");
var maxUploadSize = AppConfig.GetInt("Limits.MaxUploadSizeMB", defaultValue: 10);
var emailSender = AppConfig.Get("Email.SenderAddress", "noreply@example.com");

// Check last refresh time
var lastRefresh = AppConfig.LastRefreshTime;
```

---

## Key Design Patterns

### 1. CQRS (Command Query Responsibility Segregation)

Every operation is either a **Command** (write) or **Query** (read):

```csharp
// Command - Changes state
public record CreateUserCommand : IRequest<Result<Guid>>
{
    public string Email { get; init; }
    public string FullName { get; init; }
    public string Password { get; init; }
}

// Query - Reads state
public record GetUserQuery : IRequest<Result<UserDto>>
{
    public Guid UserId { get; init; }
}
```

### 2. Result Pattern

No exceptions for business failures - errors are explicit:

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

### 3. MediatR Pipeline Behaviors

Every command/query passes through:

1. **ValidationBehavior** - FluentValidation rules
2. **LoggingBehavior** - Request/response logging

```csharp
// Validation runs automatically before handler
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

Abstracts data access and manages transactions:

```csharp
// In a command handler
var user = User.Create(email, fullName, passwordHash);
await _unitOfWork.Users.AddAsync(user);
await _unitOfWork.SaveChangesAsync();

// With transactions
await _unitOfWork.BeginTransactionAsync();
try
{
    // Multiple operations
    await _unitOfWork.CommitTransactionAsync();
}
catch
{
    await _unitOfWork.RollbackTransactionAsync();
}
```

---

## Configuration

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

### Database Configurations

Add configurations via database that auto-refresh every minute:

```sql
INSERT INTO Configs (Id, Key, Value, Description, Category, IsActive, CreatedAt)
VALUES
  (newid(), 'Features.EnableNewUI', 'true', 'Enable new UI', 'Features', 1, getutcdate());
```

Access anywhere:
```csharp
if (AppConfig.GetBool("Features.EnableNewUI"))
{
    // Use new UI
}
```

---

## Running with Docker (Optional)

### Docker Compose for Infrastructure

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

Run: `docker-compose up -d`

---

## Adding New Features

### Example: Adding a Product Entity

1. **Domain Layer**: Create `Product.cs` entity
2. **Application Layer**:
   - Add `IRepository<Product>` to `IUnitOfWork`
   - Create `Products/Commands/CreateProduct/`
   - Create `Products/Queries/GetProducts/`
3. **Infrastructure Layer**:
   - Add `DbSet<Product>` to `ApplicationDbContext`
   - Create `ProductConfiguration.cs`
   - Add to `UnitOfWork.cs`
4. **API Layer**:
   - Create `ProductEndpoints.cs`
   - Register in `EndpointExtensions.cs`

The architecture guides you. Every feature follows the same pattern.

---

## Production Checklist

Before deploying:

- [ ] Change JWT `SecretKey` to a strong, random value
- [ ] Update connection strings for production database
- [ ] Configure Redis connection string
- [ ] Set up Seq server and update URL
- [ ] Replace simple password hashing with BCrypt
- [ ] Enable HTTPS
- [ ] Configure CORS properly (remove `AllowAll`)
- [ ] Set appropriate log levels
- [ ] Add database backup strategy
- [ ] Configure health checks
- [ ] Set up monitoring and alerting

---

## Why This Template?

### What Makes It Different

Most templates either:
- Are too simple (Hello World with no structure)
- Are too complex (enterprise monsters)
- Follow patterns blindly

**This template:**
- ✅ Production-ready from day one
- ✅ Every pattern has a clear purpose
- ✅ Code teaches through examples
- ✅ Balance between simplicity and real-world needs
- ✅ Demonstrates best practices

### Design Decisions

**Why SQLite?** Easy to start. Switch to SQL Server/PostgreSQL in 2 lines.

**Why Minimal API?** Modern, performant, less ceremony than controllers.

**Why Static AppConfig?** Blazing fast. No DI overhead. Auto-refreshes.

**Why CQRS?** Separates reads/writes. Makes code predictable.

**Why Repository Pattern?** Abstracts data access. Makes testing easier.

---

## License

MIT License - Use it, learn from it, build amazing things.

---

**Built with care. Designed to teach. Ready for production.**

*"Technology married with humanities, that yields results that make our hearts sing."*
