using Api.Extensions;
using Api.Middleware;
using Application;
using Infrastructure;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Serilog.Events;
using System.Text;

// ========================================
// SERILOG CONFIGURATION
// ========================================

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "CleanArchTemplate")
    .Enrich.WithMachineName()
    .WriteTo.Console()
    .WriteTo.Seq(
        serverUrl: Environment.GetEnvironmentVariable("SEQ_URL") ?? "http://localhost:5341",
        apiKey: Environment.GetEnvironmentVariable("SEQ_API_KEY"))
    .CreateLogger();

try
{
    Log.Information("Starting Clean Architecture API");

    var builder = WebApplication.CreateBuilder(args);

    // Use Serilog for logging
    builder.Host.UseSerilog();

    // ========================================
    // SERVICE REGISTRATION
    // ========================================

    // Add layers
    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);

    // JWT Authentication
    var jwtSettings = builder.Configuration.GetSection("Jwt");
    var secretKey = jwtSettings["SecretKey"] ?? throw new InvalidOperationException("JWT SecretKey not configured");

    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
                ValidateIssuer = true,
                ValidIssuer = jwtSettings["Issuer"] ?? "DotNetTemplate",
                ValidateAudience = true,
                ValidAudience = jwtSettings["Audience"] ?? "DotNetTemplateAPI",
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            };
        });

    builder.Services.AddAuthorization();

    // CORS
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowAll", policy =>
        {
            policy.AllowAnyOrigin()
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        });
    });

    // Swagger/OpenAPI
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new()
        {
            Title = "Clean Architecture API",
            Version = "v1",
            Description = "ASP.NET Core Minimal API with Clean Architecture, CQRS, MediatR, EF Core, Redis, JWT, and Serilog"
        });

        // JWT bearer token support in Swagger
        options.AddSecurityDefinition("Bearer", new()
        {
            Name = "Authorization",
            Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
            Scheme = "Bearer",
            BearerFormat = "JWT",
            In = Microsoft.OpenApi.Models.ParameterLocation.Header,
            Description = "Enter 'Bearer' [space] and then your token"
        });

        options.AddSecurityRequirement(new()
        {
            {
                new()
                {
                    Reference = new()
                    {
                        Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                        Id = "Bearer"
                    }
                },
                Array.Empty<string>()
            }
        });
    });

    // ========================================
    // BUILD APP
    // ========================================

    var app = builder.Build();

    // ========================================
    // DATABASE INITIALIZATION
    // ========================================

    using (var scope = app.Services.CreateScope())
    {
        await DependencyInjection.InitializeDatabaseAsync(scope.ServiceProvider);
    }

    // ========================================
    // MIDDLEWARE PIPELINE
    // ========================================

    // Exception handling (must be first)
    app.UseMiddleware<ExceptionHandlingMiddleware>();

    // Serilog request logging
    app.UseSerilogRequestLogging(options =>
    {
        options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000}ms";
        options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
        {
            diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
            diagnosticContext.Set("UserAgent", httpContext.Request.Headers["User-Agent"].ToString());
        };
    });

    // Swagger (in all environments for demo purposes)
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Clean Architecture API v1");
        options.RoutePrefix = string.Empty; // Swagger at root
    });

    // CORS
    app.UseCors("AllowAll");

    // Authentication & Authorization
    app.UseAuthentication();
    app.UseAuthorization();

    // ========================================
    // ENDPOINTS
    // ========================================

    app.MapEndpoints();

    // Health check endpoint
    app.MapGet("/health", () => Results.Ok(new
    {
        status = "Healthy",
        timestamp = DateTime.UtcNow,
        environment = builder.Environment.EnvironmentName
    }))
    .WithTags("Health")
    .WithName("HealthCheck")
    .AllowAnonymous();

    // ========================================
    // RUN
    // ========================================

    Log.Information("API started successfully on {Urls}", string.Join(", ", builder.Configuration.GetSection("urls").Value ?? "http://localhost:5000"));

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
