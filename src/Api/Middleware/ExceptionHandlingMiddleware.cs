using System.Net;
using System.Text.Json;
using FluentValidation;

namespace Api.Middleware;

/// <summary>
/// Global exception handling middleware.
/// Catches all unhandled exceptions and returns consistent error responses.
/// </summary>
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        _logger.LogError(exception, "An unhandled exception occurred: {Message}", exception.Message);

        var (statusCode, message) = exception switch
        {
            ValidationException validationEx => (
                HttpStatusCode.BadRequest,
                validationEx.Message),

            UnauthorizedAccessException => (
                HttpStatusCode.Unauthorized,
                "Unauthorized access"),

            KeyNotFoundException => (
                HttpStatusCode.NotFound,
                "Resource not found"),

            InvalidOperationException => (
                HttpStatusCode.BadRequest,
                exception.Message),

            _ => (
                HttpStatusCode.InternalServerError,
                "An internal server error occurred")
        };

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)statusCode;

        var errorResponse = new
        {
            error = message,
            statusCode = (int)statusCode,
            timestamp = DateTime.UtcNow,
            path = context.Request.Path.Value
        };

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(errorResponse, options));
    }
}
