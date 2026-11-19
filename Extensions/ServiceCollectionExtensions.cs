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
        // 1. Resolve PostgreSQL connection string from Railway environment variables in order of priority
        var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL")
            ?? Environment.GetEnvironmentVariable("DATABASE_PRIVATE_URL")
            ?? Environment.GetEnvironmentVariable("DATABASE_PUBLIC_URL")
            ?? Environment.GetEnvironmentVariable("POSTGRESQL_URL")
            ?? Environment.GetEnvironmentVariable("POSTGRES_URL");

        var defaultConn = configuration.GetConnectionString("DefaultConnection")
            ?? Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");

        string rawConn;

        if (!string.IsNullOrWhiteSpace(databaseUrl))
        {
            rawConn = databaseUrl;
        }
        else if (!string.IsNullOrWhiteSpace(defaultConn))
        {
            rawConn = defaultConn;
        }
        else
        {
            // Check individual Railway PG environment variables
            var pgHost = Environment.GetEnvironmentVariable("PGHOST") ?? Environment.GetEnvironmentVariable("POSTGRES_HOST");
            if (!string.IsNullOrWhiteSpace(pgHost))
            {
                var pgPort = Environment.GetEnvironmentVariable("PGPORT") ?? Environment.GetEnvironmentVariable("POSTGRES_PORT") ?? "5432";
                var pgDb = Environment.GetEnvironmentVariable("PGDATABASE") ?? Environment.GetEnvironmentVariable("POSTGRES_DB") ?? "railway";
                var pgUser = Environment.GetEnvironmentVariable("PGUSER") ?? Environment.GetEnvironmentVariable("POSTGRES_USER") ?? "postgres";
                var pgPass = Environment.GetEnvironmentVariable("PGPASSWORD") ?? Environment.GetEnvironmentVariable("POSTGRES_PASSWORD") ?? "";
                rawConn = $"Host={pgHost};Port={pgPort};Database={pgDb};Username={pgUser};Password={pgPass};";
            }
            else
            {
                // Fallback for local PostgreSQL development
                rawConn = "Host=localhost;Port=5432;Database=CompetitionDb;Username=postgres;Password=postgres";
            }
        }

        var npgsqlConnectionString = NormalizePostgresConnectionString(rawConn);

        // Exclusively register PostgreSqlApplicationDbContext with Npgsql provider
        services.AddDbContext<PostgreSqlApplicationDbContext>(options =>
            options.UseNpgsql(
                npgsqlConnectionString,
                postgres =>
                {
                    postgres.MigrationsAssembly(typeof(PostgreSqlApplicationDbContext).Assembly.FullName);
                    postgres.EnableRetryOnFailure(
                        maxRetryCount: 3,
                        maxRetryDelay: TimeSpan.FromSeconds(5),
                        errorCodesToAdd: null);
                }));

        // Forward resolution of ApplicationDbContext base class to PostgreSqlApplicationDbContext
        services.AddScoped<ApplicationDbContext>(serviceProvider =>
            serviceProvider.GetRequiredService<PostgreSqlApplicationDbContext>());

        // Safe diagnostic logging without exposing credentials
        var safe = GetSafeConnectionInfo(npgsqlConnectionString);
        Console.WriteLine($"Database provider: PostgreSQL (Host={safe.Host}, Port={safe.Port}, Database={safe.Database}, SSL={safe.SslMode}).");

        return services;
    }

    private static string NormalizePostgresConnectionString(string value)
    {
        NpgsqlConnectionStringBuilder builder;

        if (value.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
        {
            var uri = new Uri(value);
            var userInfo = uri.UserInfo.Split(':', 2);
            var port = uri.Port > 0 ? uri.Port : 5432;
            var database = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/'));
            var username = Uri.UnescapeDataString(userInfo[0]);
            var password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : string.Empty;

            builder = new NpgsqlConnectionStringBuilder
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
        }
        else
        {
            builder = new NpgsqlConnectionStringBuilder(value);
            if (builder.Timeout < 30) builder.Timeout = 30;
            if (builder.CommandTimeout < 60) builder.CommandTimeout = 60;
            if (builder.KeepAlive < 30) builder.KeepAlive = 30;
        }

        return builder.ConnectionString;
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
        // Standardized JWT key resolution supporting both Key and SecretKey
        var secretKey = configuration["Jwt:Key"]
            ?? configuration["Jwt:SecretKey"]
            ?? Environment.GetEnvironmentVariable("JWT_SECRET_KEY")
            ?? Environment.GetEnvironmentVariable("JWT__KEY")
            ?? "CampusConnect_DefaultDevelopmentSecretKey_MustChangeInProduction_2026!";

        var issuer = configuration["Jwt:Issuer"] ?? "CampusConnectApi";
        var audience = configuration["Jwt:Audience"] ?? "CampusConnectClient";

        var key = Encoding.UTF8.GetBytes(secretKey);

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.RequireHttpsMetadata = false;
            options.SaveToken = true;
            options.MapInboundClaims = false; // Preserves exact claim names: 'sub', 'role', 'departmentId'
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
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
