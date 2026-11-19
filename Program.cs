using System.Text.Json;
using CompetitionApi.Data;
using CompetitionApi.Data.Seed;
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

// Apply migrations and seed on startup. Fail fast on error.
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    try
    {
        logger.LogInformation("Applying PostgreSQL migrations (Provider: {Provider})...", dbContext.Database.ProviderName);
        dbContext.Database.Migrate();
        logger.LogInformation("PostgreSQL migrations applied successfully. Initializing seed data...");
        DbInitializer.Initialize(dbContext);
        logger.LogInformation("Database migration and seeding completed successfully.");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "FATAL: Database initialization failed. Exception {Type}: {Message}", ex.GetType().Name, ex.Message);
        throw;
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
