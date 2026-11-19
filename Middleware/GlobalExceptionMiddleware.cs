using System.Net;
using System.Text.Json;
using CompetitionApi.DTOs.Common;

namespace CompetitionApi.Middleware;

public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;
    private readonly IHostEnvironment _env;

    public GlobalExceptionMiddleware(
        RequestDelegate _next,
        ILogger<GlobalExceptionMiddleware> logger,
        IHostEnvironment env)
    {
        this._next = _next;
        _logger = logger;
        _env = env;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not TaskCanceledException)
    {
        _logger.LogError(ex, "Unhandled exception occurred while processing HTTP request {Method} {Path}",
            context.Request.Method, context.Request.Path);

        await HandleExceptionAsync(context, ex);
    }
    }

    private Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";

        int statusCode = exception switch
        {
            BadHttpRequestException => (int)HttpStatusCode.BadRequest,
            UnauthorizedAccessException => (int)HttpStatusCode.Unauthorized,
            KeyNotFoundException => (int)HttpStatusCode.NotFound,
            _ => (int)HttpStatusCode.InternalServerError
        };

        context.Response.StatusCode = statusCode;

        string message = statusCode switch
        {
            (int)HttpStatusCode.BadRequest => "Invalid request parameters.",
            (int)HttpStatusCode.Unauthorized => "Unauthorized access.",
            (int)HttpStatusCode.NotFound => "The requested resource was not found.",
            _ => _env.IsDevelopment()
                ? exception.Message
                : "An unexpected internal server error occurred."
        };

        var response = ApiResponse.FailResult(message);

        var json = JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        return context.Response.WriteAsync(json);
    }
}
