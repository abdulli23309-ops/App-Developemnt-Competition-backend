using CompetitionApi.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Bind to all interfaces on the port provided by the hosting platform
// (Railway sets the PORT environment variable automatically).
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrEmpty(port))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

// Add services to the container.

builder.Services.AddControllers();

// Register Entity Framework Core with SQL Server.
// Connection string is read from configuration, which supports the
// ConnectionStrings__DefaultConnection environment variable in production.
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"));
});

// CORS policy. Local development origins are always allowed. The deployed
// Vercel frontend origin is read from the FRONTEND_ORIGIN environment
// variable so it can be set per deployment without code changes.
builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendPolicy", policy =>
    {
        policy
            .WithOrigins("http://localhost:5175", "https://localhost:5175")
            .AllowAnyHeader()
            .AllowAnyMethod();

        var frontendOrigin = Environment.GetEnvironmentVariable("FRONTEND_ORIGIN");
        if (!string.IsNullOrWhiteSpace(frontendOrigin))
        {
            policy.WithOrigins(frontendOrigin.Trim());
        }
    });
});

// OpenAPI document support (built-in .NET 10 infrastructure).
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.

app.UseCors("FrontendPolicy");

// HTTPS redirection only applies when HTTPS is available (local dev).
// On Railway, traffic is terminated at the load balancer.
app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

// OpenAPI document available in Development only.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.Run();
