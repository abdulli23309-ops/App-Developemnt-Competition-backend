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
            .SetIsOriginAllowed(origin =>
            {
                if (Uri.TryCreate(origin, UriKind.Absolute, out var uri))
                {
                    return uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
                           uri.Host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase);
                }
                return false;
            })
            .AllowAnyHeader()
            .AllowAnyMethod();

        var frontendOrigin = Environment.GetEnvironmentVariable("FRONTEND_ORIGIN");
        if (!string.IsNullOrWhiteSpace(frontendOrigin))
        {
            var origins = frontendOrigin
                .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(o => o.Trim().TrimEnd('/'))
                .Where(o => !string.IsNullOrEmpty(o))
                .ToArray();

            if (origins.Length > 0)
            {
                policy.WithOrigins(origins);
            }
        }
    });
});

// OpenAPI document support (built-in .NET 10 infrastructure).
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.

app.UseCors("FrontendPolicy");

// HTTPS redirection only applies in local Development where certificates exist.
// On Railway and production container environments, TLS is terminated at the load balancer.
if (app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseAuthorization();

app.MapControllers();

// OpenAPI document available in Development only.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.Run();
