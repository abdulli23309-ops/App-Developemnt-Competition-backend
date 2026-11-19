using System.Text;
using CompetitionApi.Authorization;
using CompetitionApi.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace CompetitionApi.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDatabaseConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        // SQL Server is the primary database provider
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");

        services.AddDbContext<ApplicationDbContext>(options =>
        {
            options.UseSqlServer(connectionString);
        });

        return services;
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
