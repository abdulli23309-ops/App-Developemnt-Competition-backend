using System.Security.Claims;
using CompetitionApi.Authorization;
using CompetitionApi.Data;
using CompetitionApi.DTOs.Analytics;
using CompetitionApi.Models.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CompetitionApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AnalyticsController : BaseApiController
{
    private readonly ApplicationDbContext _context;

    public AnalyticsController(ApplicationDbContext context)
    {
        _context = context;
    }

    private (int UserId, string Role, int? DepartmentId) GetUserContext()
    {
        var sub = User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0";
        int.TryParse(sub, out var userId);
        var role = User.FindFirstValue("role") ?? User.FindFirstValue(ClaimTypes.Role) ?? Roles.Student;
        var deptClaim = User.FindFirstValue("departmentId");
        int? deptId = !string.IsNullOrEmpty(deptClaim) && int.TryParse(deptClaim, out var d) ? d : null;
        return (userId, role, deptId);
    }

    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary()
    {
        var (userId, role, userDeptId) = GetUserContext();

        var query = _context.Complaints.Include(c => c.IssueType).AsQueryable();

        if (role == Roles.Student)
        {
            query = query.Where(c => c.StudentId == userId);
        }
        else if (role == Roles.DeptAdmin && userDeptId.HasValue)
        {
            query = query.Where(c => c.DepartmentId == userDeptId.Value);
        }

        var complaintsList = await query.ToListAsync();
        var total = complaintsList.Count;
        var pending = complaintsList.Count(c => c.Status == ComplaintStatus.Pending);
        var inProgress = complaintsList.Count(c => c.Status == ComplaintStatus.InProgress || c.Status == ComplaintStatus.Reopened);
        var resolved = complaintsList.Count(c => c.Status == ComplaintStatus.Resolved);
        var rejected = complaintsList.Count(c => c.Status == ComplaintStatus.Rejected);

        var slaBreached = complaintsList.Count(c =>
        {
            int slaHours = c.IssueType?.SlaHours ?? 48;
            var deadline = c.CreatedAtUtc.AddHours(slaHours);
            return c.Status != ComplaintStatus.Resolved && DateTime.UtcNow > deadline;
        });

        double resolutionRate = total > 0 ? Math.Round((double)resolved / total * 100, 1) : 100.0;

        var resolvedComplaints = complaintsList.Where(c => c.Status == ComplaintStatus.Resolved && c.ResolvedAtUtc.HasValue).ToList();
        double avgResolutionHours = resolvedComplaints.Count > 0
            ? Math.Round(resolvedComplaints.Average(c => (c.ResolvedAtUtc!.Value - c.CreatedAtUtc).TotalHours), 1)
            : 0.0;

        return OkResult(new AnalyticsSummaryDto
        {
            TotalComplaints = total,
            PendingCount = pending,
            InProgressCount = inProgress,
            ResolvedCount = resolved,
            RejectedCount = rejected,
            SlaBreachCount = slaBreached,
            ResolutionRate = resolutionRate,
            AvgResolutionHours = avgResolutionHours
        });
    }

    [HttpGet("by-department")]
    public async Task<IActionResult> GetByDepartment()
    {
        var (_, role, userDeptId) = GetUserContext();

        var query = _context.Departments
            .Include(d => d.Complaints).ThenInclude(c => c.IssueType)
            .AsQueryable();

        if (role == Roles.DeptAdmin && userDeptId.HasValue)
        {
            query = query.Where(d => d.Id == userDeptId.Value);
        }

        var departments = await query.ToListAsync();

        var stats = departments.Select(d =>
        {
            var total = d.Complaints.Count;
            var pending = d.Complaints.Count(c => c.Status == ComplaintStatus.Pending);
            var inProgress = d.Complaints.Count(c => c.Status == ComplaintStatus.InProgress || c.Status == ComplaintStatus.Reopened);
            var resolved = d.Complaints.Count(c => c.Status == ComplaintStatus.Resolved);
            var rejected = d.Complaints.Count(c => c.Status == ComplaintStatus.Rejected);

            var resolvedComplaints = d.Complaints.Where(c => c.Status == ComplaintStatus.Resolved && c.ResolvedAtUtc.HasValue).ToList();
            var avgHours = resolvedComplaints.Count > 0
                ? Math.Round(resolvedComplaints.Average(c => (c.ResolvedAtUtc!.Value - c.CreatedAtUtc).TotalHours), 1)
                : 0.0;

            var breachedCount = d.Complaints.Count(c =>
            {
                int sla = c.IssueType?.SlaHours ?? 48;
                return c.Status != ComplaintStatus.Resolved && DateTime.UtcNow > c.CreatedAtUtc.AddHours(sla);
            });

            var breachRate = total > 0 ? Math.Round((double)breachedCount / total * 100, 1) : 0.0;

            return new DepartmentStatDto
            {
                DepartmentId = d.Id,
                DepartmentName = d.Name,
                DepartmentCode = d.Code,
                TotalComplaints = total,
                Pending = pending,
                InProgress = inProgress,
                Resolved = resolved,
                Rejected = rejected,
                AvgResolutionHours = avgHours,
                SlaBreachRate = breachRate
            };
        }).ToList();

        return OkResult(stats);
    }

    [HttpGet("trend")]
    public async Task<IActionResult> GetTrend()
    {
        var (userId, role, userDeptId) = GetUserContext();

        var query = _context.Complaints.AsQueryable();
        if (role == Roles.Student)
        {
            query = query.Where(c => c.StudentId == userId);
        }
        else if (role == Roles.DeptAdmin && userDeptId.HasValue)
        {
            query = query.Where(c => c.DepartmentId == userDeptId.Value);
        }

        var complaints = await query.ToListAsync();

        var points = new List<TrendPointDto>();
        for (int i = 6; i >= 0; i--)
        {
            var day = DateTime.UtcNow.Date.AddDays(-i);
            var nextDay = day.AddDays(1);
            var label = day.ToString("ddd, MMM dd");

            var submitted = complaints.Count(c => c.CreatedAtUtc >= day && c.CreatedAtUtc < nextDay);
            var resolved = complaints.Count(c => c.ResolvedAtUtc.HasValue && c.ResolvedAtUtc.Value >= day && c.ResolvedAtUtc.Value < nextDay);

            points.Add(new TrendPointDto
            {
                DateLabel = label,
                TotalSubmitted = submitted,
                ResolvedCount = resolved
            });
        }

        return OkResult(points);
    }

    [HttpGet("resolution-time")]
    public async Task<IActionResult> GetResolutionTime()
    {
        var departments = await _context.Departments
            .Include(d => d.Complaints)
            .ToListAsync();

        var list = departments.Select(d =>
        {
            var resolved = d.Complaints.Where(c => c.Status == ComplaintStatus.Resolved && c.ResolvedAtUtc.HasValue).ToList();
            var avg = resolved.Count > 0
                ? Math.Round(resolved.Average(c => (c.ResolvedAtUtc!.Value - c.CreatedAtUtc).TotalHours), 1)
                : 24.0;

            return new ResolutionTimeDto
            {
                DepartmentName = d.Name,
                AvgHours = avg
            };
        }).ToList();

        return OkResult(list);
    }

    [HttpGet("top-issues")]
    public async Task<IActionResult> GetTopIssues()
    {
        var issues = await _context.Complaints
            .Include(c => c.IssueType).ThenInclude(i => i!.Category)
            .Where(c => c.IssueType != null && c.IssueType.Category != null)
            .GroupBy(c => new { IssueName = c.IssueType!.Name, CategoryName = c.IssueType!.Category.Name })
            .Select(g => new TopIssueDto
            {
                IssueTypeName = g.Key.IssueName,
                CategoryName = g.Key.CategoryName,
                Count = g.Count()
            })
            .OrderByDescending(x => x.Count)
            .Take(5)
            .ToListAsync();

        return OkResult(issues);
    }
}
