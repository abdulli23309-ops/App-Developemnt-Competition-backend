using System.Text;
using CompetitionApi.Authorization;
using CompetitionApi.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Npgsql;

namespace CompetitionApi.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDatabaseConfiguration(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        // 1. Resolve PostgreSQL connection string in strict order of priority:
        //    a. DATABASE_URL env var
        //    b. ConnectionStrings:DefaultConnection
        //    c. Throw descriptive error naming missing variable
        var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
        var defaultConn = configuration.GetConnectionString("DefaultConnection")
            ?? Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");

        string npgsqlConnectionString;

        if (!string.IsNullOrWhiteSpace(databaseUrl))
        {
            npgsqlConnectionString = ConvertDatabaseUrlToNpgsqlConnectionString(databaseUrl.Trim());
        }
        else if (!string.IsNullOrWhiteSpace(defaultConn))
        {
            var trimmed = defaultConn.Trim();
            if (trimmed.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
            {
                npgsqlConnectionString = ConvertDatabaseUrlToNpgsqlConnectionString(trimmed);
            }
            else
            {
                if (!trimmed.Contains("Host=", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        "Invalid ConnectionStrings:DefaultConnection. Expected a PostgreSQL connection string starting with 'Host=' or a 'postgres://' URI.");
                }
                var csBuilder = new NpgsqlConnectionStringBuilder(trimmed);
                if (csBuilder.Timeout < 30) csBuilder.Timeout = 30;
                if (csBuilder.CommandTimeout < 60) csBuilder.CommandTimeout = 60;
                if (csBuilder.KeepAlive < 30) csBuilder.KeepAlive = 30;
                npgsqlConnectionString = csBuilder.ConnectionString;
            }
        }
        else
        {
            throw new InvalidOperationException(
                "Missing required PostgreSQL connection configuration. Set either 'DATABASE_URL' environment variable or 'ConnectionStrings:DefaultConnection' configuration.");
        }

        // Register ONE concrete ApplicationDbContext with Npgsql provider
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(
                npgsqlConnectionString,
                postgres =>
                {
                    postgres.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName);
                    postgres.EnableRetryOnFailure(
                        maxRetryCount: 3,
                        maxRetryDelay: TimeSpan.FromSeconds(5),
                        errorCodesToAdd: null);
                }));

        // Safe diagnostic logging without exposing passwords or credentials
        var safe = GetSafeConnectionInfo(npgsqlConnectionString);
        Console.WriteLine($"Database provider: PostgreSQL (Host={safe.Host}, Port={safe.Port}, Database={safe.Database}, SSL={safe.SslMode}).");

        return services;
    }

    public static string ConvertDatabaseUrlToNpgsqlConnectionString(string databaseUrl)
    {
        if (databaseUrl.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) ||
            databaseUrl.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
        {
            var uri = new Uri(databaseUrl);
            var userInfo = uri.UserInfo.Split(':', 2);
            var port = uri.Port > 0 ? uri.Port : 5432;
            var database = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/'));
            var username = Uri.UnescapeDataString(userInfo[0]);
            var password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : string.Empty;

            var builder = new NpgsqlConnectionStringBuilder
            {
                Host = uri.Host,
                Port = port,
                Database = string.IsNullOrWhiteSpace(database) ? "railway" : database,
                Username = username,
                Password = password,
                SslMode = SslMode.Prefer,
                Timeout = 30,
                CommandTimeout = 60,
                KeepAlive = 30
            };

            foreach (var parameter in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var pair = parameter.Split('=', 2);
                if (pair.Length == 2 && pair[0].Equals("sslmode", StringComparison.OrdinalIgnoreCase))
                {
                    if (Enum.TryParse<SslMode>(Uri.UnescapeDataString(pair[1]), true, out var parsedMode))
                    {
                        builder.SslMode = parsedMode;
                    }
                }
            }

            return builder.ConnectionString;
        }

        // If already key=value format
        var kvBuilder = new NpgsqlConnectionStringBuilder(databaseUrl);
        if (kvBuilder.Timeout < 30) kvBuilder.Timeout = 30;
        if (kvBuilder.CommandTimeout < 60) kvBuilder.CommandTimeout = 60;
        if (kvBuilder.KeepAlive < 30) kvBuilder.KeepAlive = 30;
        return kvBuilder.ConnectionString;
    }

    private static (string Host, int Port, string Database, string SslMode) GetSafeConnectionInfo(string connectionString)
    {
        try
        {
            var csBuilder = new NpgsqlConnectionStringBuilder(connectionString);
            return (csBuilder.Host ?? "unknown", csBuilder.Port, csBuilder.Database ?? "unknown", csBuilder.SslMode.ToString());
        }
        catch
        {
            return ("parsed", 5432, "unknown", "unknown");
        }
    }

    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var secretKey = configuration["Jwt:Key"]
            ?? configuration["Jwt:SecretKey"]
            ?? Environment.GetEnvironmentVariable("JWT_SECRET_KEY")
            ?? Environment.GetEnvironmentVariable("JWT__KEY")
            ?? "CampusConnect_DefaultDevelopmentSecretKey_MustChangeInProduction_2026!";

        var issuer = configuration["Jwt:Issuer"]
            ?? Environment.GetEnvironmentVariable("JWT__ISSUER")
            ?? "CampusConnectApi";

        var audience = configuration["Jwt:Audience"]
            ?? Environment.GetEnvironmentVariable("JWT__AUDIENCE")
            ?? "CampusConnectClient";

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = key,
                    ValidateIssuer = !string.IsNullOrEmpty(issuer),
                    ValidIssuer = issuer,
                    ValidateAudience = !string.IsNullOrEmpty(audience),
                    ValidAudience = audience,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                };
            });

        services.AddAuthorization(options =>
        {
            options.AddPolicy(Policies.RequireStudent, policy => policy.RequireRole(Roles.Student));
            options.AddPolicy(Policies.RequireDeptAdmin, policy => policy.RequireRole(Roles.DeptAdmin));
            options.AddPolicy(Policies.RequireSuperAdmin, policy => policy.RequireRole(Roles.SuperAdmin));
        });

        return services;
    }
}
