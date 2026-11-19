using CompetitionApi.DTOs.Auth;

namespace CompetitionApi.Services.Interfaces;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request);
    Task<AuthResponse> LoginAsync(LoginRequest request);
    Task<UserDto?> GetMeAsync(int userId);
    Task<List<DemoUserDto>> GetDemoUsersAsync();
}