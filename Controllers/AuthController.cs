using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using CompetitionApi.Data;
using CompetitionApi.DTOs.Common;
using CompetitionApi.Authorization;
using CompetitionApi.DTOs.Auth;
using CompetitionApi.Models.Entities;
using CompetitionApi.Models.Enums;
using CompetitionApi.Services.Interfaces;
using Microsoft.AspNetCore.Identity;

namespace CompetitionApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : BaseApiController
{
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _config;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly IAuthService _authService;

    public AuthController(
        ApplicationDbContext context,
        IConfiguration config,
        IPasswordHasher<User> passwordHasher,
        IAuthService authService)
    {
        _context = context;
        _config = config;
        _passwordHasher = passwordHasher;
        _authService = authService;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        try
        {
            var result = await _authService.LoginAsync(req);
            return OkResult(result, "Login successful.");
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized(ApiResponse.FailResult("Invalid email or password."));
        }
    }

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register([FromBody] RegisterRequest req)
    {
        try
        {
            var result = await _authService.RegisterAsync(req);
            return OkResult(result, "Registration successful.");
        }
        catch (InvalidOperationException ex)
        {
            return BadRequestResult(ex.Message);
        }
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> GetCurrentUser()
    {
        var userId = User.GetUserId();
        if (userId == null)
            return Unauthorized(ApiResponse.FailResult("Not authenticated."));

        var user = await _authService.GetMeAsync(userId.Value);
        if (user == null)
            return NotFoundResult("User not found.");

        return OkResult(user);
    }

    [HttpGet("demo-users")]
    [AllowAnonymous]
    public async Task<IActionResult> GetDemoUsers()
    {
        var users = await _authService.GetDemoUsersAsync();
        return OkResult(users);
    }
}
