using CompetitionApi.Data;
using CompetitionApi.DTOs.Metadata;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CompetitionApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MetaController : BaseApiController
{
    private readonly ApplicationDbContext _context;

    public MetaController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet("categories")]
    public async Task<IActionResult> GetCategories()
    {
        var categories = await _context.Categories
            .Include(c => c.Department)
            .Include(c => c.IssueTypes)
            .OrderBy(c => c.Id)
            .Select(c => new CategoryDto
            {
                Id = c.Id,
                Name = c.Name,
                Description = c.Description,
                DepartmentId = c.DepartmentId,
                DepartmentName = c.Department.Name,
                DepartmentCode = c.Department.Code,
                IssueTypes = c.IssueTypes.Select(i => new IssueTypeDto
                {
                    Id = i.Id,
                    Name = i.Name,
                    DefaultPriority = i.DefaultPriority.ToString(),
                    SlaHours = i.SlaHours
                }).ToList()
            })
            .ToListAsync();

        return OkResult(categories);
    }

    [HttpGet("departments")]
    public async Task<IActionResult> GetDepartments()
    {
        var departments = await _context.Departments
            .OrderBy(d => d.Id)
            .Select(d => new DepartmentDto
            {
                Id = d.Id,
                Name = d.Name,
                Code = d.Code,
                Description = d.Description
            })
            .ToListAsync();

        return OkResult(departments);
    }
}
