using System.Security.Claims;

namespace CompetitionApi.Authorization;

public static class ClaimsPrincipalExtensions
{
    public static int? GetUserId(this ClaimsPrincipal user)
    {
        if (user == null) return null;
        var sub = user.FindFirst("sub")?.Value ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(sub, out var id) ? id : null;
    }

    public static string? GetRole(this ClaimsPrincipal user)
    {
        if (user == null) return null;
        return user.FindFirst(ClaimTypes.Role)?.Value
            ?? user.FindFirst("role")?.Value;
    }

    public static int? GetDepartmentId(this ClaimsPrincipal user)
    {
        if (user == null) return null;
        var val = user.FindFirst("departmentId")?.Value;
        return int.TryParse(val, out var id) ? id : null;
    }

    public static bool IsInRoleExact(this ClaimsPrincipal user, string role)
    {
        return string.Equals(GetRole(user), role, StringComparison.OrdinalIgnoreCase);
    }
}