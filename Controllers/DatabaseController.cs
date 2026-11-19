using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using CompetitionApi.Data;

namespace CompetitionApi.Controllers;

[ApiController]
[Route("api/database")]
public class DatabaseController : ControllerBase
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<DatabaseController> _logger;

    public DatabaseController(ApplicationDbContext dbContext, ILogger<DatabaseController> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    [HttpGet("status")]
    public async Task<IActionResult> GetStatus(CancellationToken cancellationToken = default)
    {
        try
        {
            bool canConnect = await _dbContext.Database.CanConnectAsync(cancellationToken);

            if (canConnect)
            {
                _logger.LogInformation("Database connectivity check succeeded at {Timestamp}. Provider={Provider}",
                    DateTime.UtcNow, _dbContext.Database.ProviderName);
                return Ok(new
                {
                    success = true,
                    message = "Database is reachable.",
                    provider = _dbContext.Database.ProviderName,
                    timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss'Z'")
                });
            }

            _logger.LogWarning("Database connectivity check returned false at {Timestamp}. Provider={Provider}",
                DateTime.UtcNow, _dbContext.Database.ProviderName);
            return StatusCode(503, new
            {
                success = false,
                message = "Database is unavailable.",
                provider = _dbContext.Database.ProviderName,
                timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss'Z'")
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database connectivity check threw an exception at {Timestamp}. Provider={Provider}, Message={Message}",
                DateTime.UtcNow, _dbContext.Database.ProviderName, ex.Message);
            return StatusCode(503, new
            {
                success = false,
                message = "Database is unavailable.",
                provider = _dbContext.Database.ProviderName,
                error = ex.Message,
                timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss'Z'")
            });
        }
    }
}
