using CompetitionApi.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CompetitionApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[AllowAnonymous]
public class VersionController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private static readonly DateTime BuildTimeUtc = DateTime.UtcNow;

    public VersionController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public IActionResult GetVersion()
    {
        var commitSha = Environment.GetEnvironmentVariable("RAILWAY_GIT_COMMIT_SHA")
            ?? Environment.GetEnvironmentVariable("GIT_COMMIT_SHA")
            ?? "local";

        var provider = _context.Database.ProviderName ?? "Unknown";

        return Ok(new
        {
            success = true,
            commitSha = commitSha,
            provider = provider,
            buildTime = BuildTimeUtc.ToString("yyyy-MM-ddTHH:mm:ss'Z'")
        });
    }
}
