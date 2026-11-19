using Microsoft.AspNetCore.Mvc;
using CompetitionApi.DTOs.Common;

namespace CompetitionApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public abstract class BaseApiController : ControllerBase
{
    protected IActionResult OkResult<T>(T data, string? message = null)
    {
        return Ok(ApiResponse<T>.Ok(data, message));
    }

    protected IActionResult OkResult(string? message = null)
    {
        return Ok(ApiResponse.OkResult(message));
    }

    protected IActionResult BadRequestResult(string message, IEnumerable<string>? errors = null)
    {
        return BadRequest(ApiResponse.FailResult(message, errors));
    }

    protected IActionResult NotFoundResult(string message = "The requested resource was not found.")
    {
        return NotFound(ApiResponse.FailResult(message));
    }
}
