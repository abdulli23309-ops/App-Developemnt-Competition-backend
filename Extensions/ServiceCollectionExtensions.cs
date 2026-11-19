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
        var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
        var defaultConn = configuration.GetConnectionString("DefaultConnection")
            ?? Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");

        var rawConn = !string.IsNullOrWhiteSpace(databaseUrl)
            ? databaseUrl
            : !string.IsNullOrWhiteSpace(defaultConn)
                ? defaultConn
                : "Host=localhost;Port=5432;Database=CompetitionDb;Username=postgres;Password=postgres";

        var npgsqlConnectionString = NormalizePostgresConnectionString(rawConn);

        // Exclusively register PostgreSqlApplicationDbContext with Npgsql provider
        services.AddDbContext<PostgreSqlApplicationDbContext>(options =>
            options.UseNpgsql(
                npgsqlConnectionString,
                postgres => postgres.MigrationsAssembly(typeof(PostgreSqlApplicationDbContext).Assembly.FullName)));

        // Forward resolution of ApplicationDbContext base class to PostgreSqlApplicationDbContext
        services.AddScoped<ApplicationDbContext>(serviceProvider =>
            serviceProvider.GetRequiredService<PostgreSqlApplicationDbContext>());

        Console.WriteLine("Database provider selected: PostgreSQL (Npgsql exclusively configured).");

        return services;
    }

    private static string NormalizePostgresConnectionString(string value)
    {
        if (!value.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) &&
            !value.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
        {
            var csBuilder = new NpgsqlConnectionStringBuilder(value);
            return csBuilder.ConnectionString;
        }

        var uri = new Uri(value);
        var userInfo = uri.UserInfo.Split(':', 2);
        var uriBuilder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.IsDefaultPort ? 5432 : uri.Port,
            Database = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/')),
            Username = Uri.UnescapeDataString(userInfo[0]),
            Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : string.Empty,
            SslMode = SslMode.Prefer
        };

        foreach (var parameter in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var pair = parameter.Split('=', 2);
            if (pair.Length == 2 && pair[0].Equals("sslmode", StringComparison.OrdinalIgnoreCase))
            {
                if (Enum.TryParse<SslMode>(Uri.UnescapeDataString(pair[1]), true, out var parsedMode))
                {
                    uriBuilder.SslMode = parsedMode;
                }
            }
        }

        return uriBuilder.ConnectionString;
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
