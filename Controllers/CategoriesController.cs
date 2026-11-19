using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CompetitionApi.Data;

namespace CompetitionApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CategoriesController : BaseApiController
{
    private readonly ApplicationDbContext _context;

    public CategoriesController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetCategories()
    {
        var categories = await _context.Categories
            .Include(c => c.Department)
            .Include(c => c.IssueTypes)
            .ToListAsync();

        var result = categories.Select(c =>
        {
            var deptCode = c.Department?.Code?.ToUpper() ?? "";
            string icon = deptCode switch
            {
                "CAFE" => "UtensilsCrossed",
                "LIB" => "BookOpen",
                "IT_LABS" => "Server",
                "ACADEMIC" => "GraduationCap",
                _ => "HelpCircle"
            };

            var chips = deptCode switch
            {
                "CAFE" => new List<string> { "Food quality", "Hygiene", "Pricing", "Staff behaviour" },
                "LIB" => new List<string> { "Book availability", "Facilities", "Seating", "Quiet zone" },
                "IT_LABS" => new List<string> { "Computer problems", "Internet connectivity", "Projectors", "Software" },
                "ACADEMIC" => new List<string> { "Lecture notes", "Teaching quality", "Classroom management" },
                _ => new List<string> { "General", "Urgent" }
            };

            var specific = c.IssueTypes.Select(i => i.Name).ToList();
            if (specific.Count == 0)
            {
                specific = chips;
            }

            return new
            {
                id = c.Id,
                name = c.Name,
                description = c.Description,
                icon = icon,
                departmentId = c.DepartmentId,
                departmentName = c.Department?.Name ?? "",
                issueChips = chips,
                specificIssues = specific
            };
        }).ToList();

        return OkResult(result);
    }
}
