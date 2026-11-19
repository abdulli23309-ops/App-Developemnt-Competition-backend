using CompetitionApi.Data;
using CompetitionApi.Extensions;
using CompetitionApi.Middleware;
using CompetitionApi.Models.Entities;
using CompetitionApi.Services;
using CompetitionApi.Services.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Bind to port provided by hosting environment (e.g., Railway PORT)
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrEmpty(port))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

// 1. Controller & API Services
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    });

// 2. Database Configuration (SQL Server primary provider)
builder.Services.AddDatabaseConfiguration(builder.Configuration);

// HTTP context accessor for scoped complaint queries
builder.Services.AddHttpContextAccessor();

// Application services
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IComplaintService, ComplaintService>();

// 3. JWT Authentication & Authorization
builder.Services.AddJwtAuthentication(builder.Configuration);

// 4. CORS Configuration
builder.Services.AddCorsConfiguration();

// 5. OpenAPI Documentation
builder.Services.AddOpenApi();

var app = builder.Build();

// --- Centralized Exception Handling Middleware ---
app.UseMiddleware<GlobalExceptionMiddleware>();

// --- Database Migration & Seed on Startup ---
using (var scope = app.Services.CreateScope())
{
    try
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        CompetitionApi.Data.Seed.DbInitializer.Initialize(dbContext);
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Database startup initialization/seeding failed or skipped: {Message}", ex.Message);
    }
}

// Security & Routing Middleware Pipeline
app.UseCors("FrontendPolicy");

if (app.Environment.IsDevelopment())
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
