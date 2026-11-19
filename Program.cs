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

// Apply migrations and seed on startup. Fail startup if either step fails.
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

    // The checked-in migration set was scaffolded for SQL Server. EF migrations contain
    // provider-specific types and annotations, so do not apply it to Railway PostgreSQL.
    if (dbContext.Database.ProviderName == "Npgsql.EntityFrameworkCore.PostgreSQL")
    {
        throw new InvalidOperationException(
            "PostgreSQL was selected, but this deployment has no PostgreSQL-specific EF Core migrations. " +
            "Refusing to migrate or seed the database until a PostgreSQL migration set is added.");
    }

    dbContext.Database.Migrate();
    CompetitionApi.Data.Seed.DbInitializer.Initialize(dbContext);
}

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
