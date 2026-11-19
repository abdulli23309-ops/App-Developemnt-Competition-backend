using System.Text.Json;
using CompetitionApi.Data;
using CompetitionApi.Extensions;
using CompetitionApi.Middleware;
using CompetitionApi.Models.Entities;
using CompetitionApi.Services;
using CompetitionApi.Services.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrEmpty(port))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    });

builder.Services.AddDatabaseConfiguration(builder.Configuration, builder.Environment);

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IComplaintService, ComplaintService>();
builder.Services.AddJwtAuthentication(builder.Configuration);

// CORS: Localhost + Vercel FRONTEND_ORIGIN
var allowedOrigins = new List<string>
{
    "http://localhost:5175",
    "https://localhost:5175",
    "http://localhost:5173",
    "https://localhost:5173"
};

var frontendOrigin = Environment.GetEnvironmentVariable("FRONTEND_ORIGIN");
if (!string.IsNullOrWhiteSpace(frontendOrigin))
{
    var cleanOrigin = frontendOrigin.Trim().TrimEnd('/');
    if (!allowedOrigins.Contains(cleanOrigin, StringComparer.OrdinalIgnoreCase))
    {
        allowedOrigins.Add(cleanOrigin);
    }
}

builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendPolicy", policy =>
    {
        policy
            .WithOrigins(allowedOrigins.ToArray())
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

builder.Services.AddOpenApi();

var app = builder.Build();

app.UseMiddleware<GlobalExceptionMiddleware>();

// Apply migrations and seed on startup exclusively using PostgreSqlApplicationDbContext.
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<PostgreSqlApplicationDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    const int maxRetries = 5;
    var succeeded = false;

    for (var attempt = 1; attempt <= maxRetries; attempt++)
    {
        try
        {
            logger.LogInformation("Connecting to PostgreSQL and verifying migrations (attempt {Attempt}/{MaxRetries})...", attempt, maxRetries);
            dbContext.Database.Migrate();
            CompetitionApi.Data.Seed.DbInitializer.Initialize(dbContext);
            succeeded = true;
            logger.LogInformation("PostgreSQL migration and database seeding completed successfully.");
            break;
        }
        catch (Exception ex) when (attempt < maxRetries)
        {
            logger.LogWarning(ex, "Database connection/migration attempt {Attempt} failed: {Message}. Retrying in 2 seconds...", attempt, ex.Message);
            Thread.Sleep(2000);
        }
    }

    if (!succeeded)
    {
        logger.LogError("CRITICAL: Failed to connect to or migrate PostgreSQL database after {MaxRetries} attempts.", maxRetries);
        throw new InvalidOperationException($"CRITICAL: Database initialization failed after {maxRetries} attempts.");
    }
}

app.UseCors("FrontendPolicy");

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.Run();
