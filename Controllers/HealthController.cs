using Microsoft.AspNetCore.Mvc;

namespace CompetitionApi.Controllers;

[ApiController]
[Route("api/health")]
public class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        return Ok(new
        {
            success = true,
            message = "Competition API is running!",
            timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss'Z'")
        });
    }
}
