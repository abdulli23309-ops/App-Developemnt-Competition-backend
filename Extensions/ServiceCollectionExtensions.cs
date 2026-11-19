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

        var hasPostgresUrl = !string.IsNullOrWhiteSpace(databaseUrl);
        var hasPostgresDefault = IsPostgresConnection(defaultConn);
        var isPostgres = hasPostgresUrl || hasPostgresDefault;

        if (environment.IsProduction())
        {
            if (!isPostgres)
            {
                throw new InvalidOperationException(
                    "CRITICAL: Production requires a valid PostgreSQL configuration via DATABASE_URL or ConnectionStrings__DefaultConnection. " +
                    "Silent fallback to SQL Server is strictly forbidden in production.");
            }

            var rawConn = hasPostgresUrl ? databaseUrl! : defaultConn!;
            var npgsqlConnectionString = NormalizePostgresConnectionString(rawConn);
            services.AddDbContext<ApplicationDbContext>(options => options.UseNpgsql(npgsqlConnectionString));
            Console.WriteLine("Database provider selected: PostgreSQL (Railway Production).");
        }
        else if (isPostgres)
        {
            var rawConn = hasPostgresUrl ? databaseUrl! : defaultConn!;
            var npgsqlConnectionString = NormalizePostgresConnectionString(rawConn);
            services.AddDbContext<ApplicationDbContext>(options => options.UseNpgsql(npgsqlConnectionString));
            Console.WriteLine("Database provider selected: PostgreSQL (Development).");
        }
        else
        {
            var sqlServerConn = !string.IsNullOrWhiteSpace(defaultConn)
                ? defaultConn
                : "Server=DESKTOP-UD649GB\\SQLEXPRESS;Database=CompetitionDb;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True";

            services.AddDbContext<ApplicationDbContext>(options => options.UseSqlServer(sqlServerConn));
            Console.WriteLine("Database provider selected: SQL Server (Local Development).");
        }

        return services;
    }

    private static bool IsPostgresConnection(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        (value.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) ||
         value.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase) ||
         value.Contains("Host=", StringComparison.OrdinalIgnoreCase));

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
                ClockSkew = TimeSpan.Zero,
                NameClaimType = "sub",
                RoleClaimType = "role"
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

    public static IServiceCollection AddCorsConfiguration(this IServiceCollection services)
    {
        services.AddCors(options =>
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

        return services;
    }
}
