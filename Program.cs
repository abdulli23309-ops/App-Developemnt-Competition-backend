using CompetitionApi.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

// Bind to all interfaces on the port provided by the hosting platform
// (Railway sets the PORT environment variable automatically).
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrEmpty(port))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

builder.Services.AddControllers();

// --- DATABASE CONFIGURATION (Local SQL Server vs Railway PostgreSQL) ---
var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");

if (string.IsNullOrEmpty(databaseUrl))
{
    // Local Development: Use SQL Server from appsettings.json
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
    {
        options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"));
    });
}
else
{
    // Railway Production: Use PostgreSQL from Railway DATABASE_URL
    var connectionString = ConvertPostgreSqlUrlToConnectionString(databaseUrl);
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
    {
        options.UseNpgsql(connectionString);
    });
}
// ------------------------------------------------------------------------

// CORS policy configuration
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

builder.Services.AddOpenApi();

var app = builder.Build();

// --- AUTOMATIC DATABASE INITIALIZATION / MIGRATION ON STARTUP ---
using (var scope = app.Services.CreateScope())
{
    try
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        // Automatically applies pending migrations if any exist, or creates schema
        var pendingMigrations = dbContext.Database.GetPendingMigrations();
        if (pendingMigrations.Any())
        {
            dbContext.Database.Migrate();
        }
        else
        {
            dbContext.Database.EnsureCreated();
        }
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Database startup initialization/migration failed or skipped.");
    }
}
// ---------------------------------------------------------------

app.UseCors("FrontendPolicy");

if (app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseAuthorization();

app.MapControllers();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.Run();

// Helper to convert PostgreSQL URIs (e.g. postgres://user:pass@host:port/db) to Npgsql connection string
static string ConvertPostgreSqlUrlToConnectionString(string url)
{
    if (!url.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) &&
        !url.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
    {
        return url;
    }

    try
    {
        var uri = new Uri(url);
        var userInfo = uri.UserInfo.Split(':');
        var username = userInfo.Length > 0 ? Uri.UnescapeDataString(userInfo[0]) : "";
        var password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : "";
        var host = uri.Host;
        var port = uri.Port > 0 ? uri.Port : 5432;
        var database = uri.AbsolutePath.TrimStart('/');

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = host,
            Port = port,
            Database = database,
            Username = username,
            Password = password,
            SslMode = SslMode.Prefer
        };

        return builder.ConnectionString;
    }
    catch
    {
        return url;
    }
}
