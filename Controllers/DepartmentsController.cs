using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CompetitionApi.Data;

namespace CompetitionApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DepartmentsController : BaseApiController
{
    private readonly ApplicationDbContext _context;

    public DepartmentsController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetDepartments()
    {
        var depts = await _context.Departments
            .Select(d => new
            {
                id = d.Id,
                name = d.Name,
                code = d.Code,
                description = d.Description
            })
            .ToListAsync();

        return OkResult(depts);
    }
}
