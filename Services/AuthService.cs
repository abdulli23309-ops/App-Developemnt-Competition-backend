using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using CompetitionApi.Authorization;
using CompetitionApi.Data;
using CompetitionApi.DTOs.Auth;
using CompetitionApi.Models.Entities;
using CompetitionApi.Models.Enums;
using CompetitionApi.Services.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace CompetitionApi.Services;

public class AuthService : IAuthService
{
    private readonly ApplicationDbContext _db;
    private readonly IConfiguration _configuration;
    private readonly IPasswordHasher<User> _passwordHasher;

    public AuthService(
        ApplicationDbContext db,
        IConfiguration configuration,
        IPasswordHasher<User> passwordHasher)
    {
        _db = db;
        _configuration = configuration;
        _passwordHasher = passwordHasher;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FullName))
            throw new InvalidOperationException("Full name is required.");

        if (await _db.Users.AnyAsync(u => u.Email == request.Email))
            throw new InvalidOperationException("Email already registered.");

        if (!string.IsNullOrWhiteSpace(request.RegistrationNumber) &&
            await _db.Users.AnyAsync(u => u.RegistrationNumber == request.RegistrationNumber))
            throw new InvalidOperationException("Registration number already in use.");

        // Public registration creates Student accounts only. Never allow self-selection of privileged roles.
        var user = new User
        {
            FullName = request.FullName,
            Email = request.Email,
            RegistrationNumber = request.RegistrationNumber,
            AcademicProgram = request.AcademicProgram,
            Role = UserRole.Student,
            DepartmentId = request.DepartmentId,
            CreatedAtUtc = DateTime.UtcNow,
        };
        user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        return await BuildAuthResponseAsync(user);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request)
    {
        var user = await _db.Users
            .Include(u => u.Department)
            .FirstOrDefaultAsync(u => u.Email == request.Email);

        if (user == null)
            throw new UnauthorizedAccessException("Invalid email or password.");

        var result = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (result == PasswordVerificationResult.Failed)
            throw new UnauthorizedAccessException("Invalid email or password.");

        return await BuildAuthResponseAsync(user);
    }

    public async Task<UserDto?> GetMeAsync(int userId)
    {
        return await _db.Users
            .Include(u => u.Department)
            .Where(u => u.Id == userId)
            .Select(u => new UserDto
            {
                Id = u.Id,
                FullName = u.FullName,
                Email = u.Email,
                RegistrationNumber = u.RegistrationNumber,
                AcademicProgram = u.AcademicProgram,
                Role = u.Role.ToString(),
                DepartmentId = u.DepartmentId,
                DepartmentName = u.Department != null ? u.Department.Name : null,
                CreatedAtUtc = u.CreatedAtUtc,
            })
            .FirstOrDefaultAsync();
    }

    public async Task<List<DemoUserDto>> GetDemoUsersAsync()
    {
        var users = await _db.Users
            .Where(u => u.Role != UserRole.Student)
            .OrderBy(u => u.Role)
            .ThenBy(u => u.FullName)
            .ToListAsync();

        return users.Select(u => new DemoUserDto
        {
            Title = u.Role switch
            {
                UserRole.SuperAdmin => "Super Admin",
                UserRole.DeptAdmin => "Dept Admin",
                _ => u.Role.ToString()
            },
            Name = u.FullName,
            Email = u.Email,
            RegNo = u.RegistrationNumber ?? string.Empty,
        }).ToList();
    }

    private async Task<AuthResponse> BuildAuthResponseAsync(User user)
    {
        await _db.Entry(user).Reference(u => u.Department).LoadAsync();

        var token = GenerateJwtToken(user);
        return new AuthResponse
        {
            Token = token,
            User = new UserDto
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email,
                RegistrationNumber = user.RegistrationNumber,
                AcademicProgram = user.AcademicProgram,
                Role = user.Role.ToString(),
                DepartmentId = user.DepartmentId,
                DepartmentName = user.Department?.Name,
                CreatedAtUtc = user.CreatedAtUtc,
            }
        };
    }

    private string GenerateJwtToken(User user)
    {
        var secretKey = _configuration["Jwt:Key"]
            ?? _configuration["Jwt:SecretKey"]
            ?? Environment.GetEnvironmentVariable("JWT_SECRET_KEY")
            ?? Environment.GetEnvironmentVariable("JWT__KEY")
            ?? "CampusConnect_DefaultDevelopmentSecretKey_MustChangeInProduction_2026!";

        var issuer = _configuration["Jwt:Issuer"] ?? "CampusConnectApi";
        var audience = _configuration["Jwt:Audience"] ?? "CampusConnectClient";
        var expiryMinutes = int.TryParse(
            _configuration["Jwt:ExpiryMinutes"] ?? Environment.GetEnvironmentVariable("JWT__EXPIRYMINUTES"),
            out var mins) ? mins : 1440;

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new Claim("sub", user.Id.ToString()),
            new Claim("role", user.Role.ToString()),
            new Claim(ClaimTypes.Role, user.Role.ToString()),
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.FullName),
            new Claim(ClaimTypes.Email, user.Email),
        };

        if (user.DepartmentId.HasValue)
        {
            claims.Add(new Claim("departmentId", user.DepartmentId.Value.ToString()));
        }

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expiryMinutes),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}