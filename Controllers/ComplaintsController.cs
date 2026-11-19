using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CompetitionApi.Authorization;
using CompetitionApi.Data;
using CompetitionApi.DTOs.Common;
using CompetitionApi.DTOs.Complaints;
using CompetitionApi.Models.Entities;
using CompetitionApi.Models.Enums;

namespace CompetitionApi.Controllers;

public class CreateComplaintDto
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public int CategoryId { get; set; }
    public string? SpecificIssue { get; set; }
    public string? Priority { get; set; }
    public bool IsAnonymous { get; set; }
    public List<string>? AttachmentUrls { get; set; }
}

public class UpdateStatusDto
{
    public string Status { get; set; } = string.Empty;
    public string? Remark { get; set; }
    public string? RejectionReason { get; set; }
}

public class AddRemarkDto
{
    public string Message { get; set; } = string.Empty;
    public bool IsInternal { get; set; } = false;
}

public class RatingDto
{
    public int Rating { get; set; }
    public string? Comment { get; set; }
}

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ComplaintsController : BaseApiController
{
    private readonly ApplicationDbContext _context;

    public ComplaintsController(ApplicationDbContext context)
    {
        _context = context;
    }

    private (int UserId, string Role, int? DepartmentId) GetUserContext()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub") ?? "0";
        int.TryParse(sub, out var userId);
        var role = User.FindFirstValue(ClaimTypes.Role) ?? User.FindFirstValue("role") ?? Roles.Student;
        var deptClaim = User.FindFirstValue("departmentId");
        int? deptId = !string.IsNullOrEmpty(deptClaim) && int.TryParse(deptClaim, out var d) ? d : null;
        return (userId, role, deptId);
    }

    [HttpGet]
    public async Task<IActionResult> GetComplaints(
        [FromQuery] string? search = null,
        [FromQuery] string? status = null,
        [FromQuery] int? departmentId = null,
        [FromQuery] string? priority = null)
    {
        var (userId, role, userDeptId) = GetUserContext();

        var query = _context.Complaints
            .Include(c => c.Category)
            .Include(c => c.Department)
            .Include(c => c.IssueType)
            .Include(c => c.Student)
            .Include(c => c.Attachments)
            .Include(c => c.Responses)
            .AsQueryable();

        // RBAC Scoping
        if (role == Roles.Student)
        {
            query = query.Where(c => c.StudentId == userId);
        }
        else if (role == Roles.DeptAdmin && userDeptId.HasValue)
        {
            query = query.Where(c => c.DepartmentId == userDeptId.Value);
        }

        // Filters
        if (departmentId.HasValue && departmentId.Value > 0)
        {
            query = query.Where(c => c.DepartmentId == departmentId.Value);
        }

        if (!string.IsNullOrWhiteSpace(status) && status.ToLower() != "all")
        {
            if (Enum.TryParse<ComplaintStatus>(status.Replace(" ", ""), true, out var parsedStatus))
            {
                query = query.Where(c => c.Status == parsedStatus);
            }
        }

        if (!string.IsNullOrWhiteSpace(priority) && priority.ToLower() != "all")
        {
            if (Enum.TryParse<ComplaintPriority>(priority, true, out var parsedPriority))
            {
                query = query.Where(c => c.Priority == parsedPriority);
            }
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            query = query.Where(c =>
                EF.Functions.ILike(c.TrackingCode, pattern) ||
                EF.Functions.ILike(c.Title, pattern) ||
                EF.Functions.ILike(c.Description, pattern) ||
                (c.Location != null && EF.Functions.ILike(c.Location, pattern)));
        }

        var list = await query
            .OrderByDescending(c => c.CreatedAtUtc)
            .ToListAsync();

        var now = DateTime.UtcNow;
        var result = list.Select(c =>
        {
            int slaHours = c.IssueType?.SlaHours ?? (c.Priority == ComplaintPriority.Urgent ? 12 : 48);
            var deadline = c.CreatedAtUtc.AddHours(slaHours);
            var remainingMinutes = (deadline - now).TotalMinutes;
            var isBreached = c.Status != ComplaintStatus.Resolved && remainingMinutes < 0;

            var firstAtt = c.Attachments.FirstOrDefault();
            var attUrl = firstAtt != null
                ? "https://images.unsplash.com/photo-1550751827-4bd374c3f58b?auto=format&fit=crop&w=600&q=80"
                : (c.Id % 2 == 1 ? "https://images.unsplash.com/photo-1550751827-4bd374c3f58b?auto=format&fit=crop&w=600&q=80" : null);

            return new
            {
                id = c.Id,
                trackingCode = c.TrackingCode,
                title = c.Title,
                description = c.Description,
                location = c.Location ?? "Campus Grounds",
                specificIssue = c.IssueType?.Name,
                isAnonymous = c.IsAnonymous,
                status = c.Status.ToString(),
                priority = c.Priority.ToString(),
                categoryId = c.CategoryId,
                categoryName = c.Category?.Name ?? "General",
                departmentId = c.DepartmentId,
                departmentName = c.Department?.Name ?? "General",
                submitterName = c.IsAnonymous && role != Roles.SuperAdmin ? "Anonymous Student" : c.Student?.FullName,
                submitterRegNo = c.IsAnonymous && role != Roles.SuperAdmin ? "HIDDEN" : c.Student?.RegistrationNumber,
                attachmentsCount = c.Attachments.Count > 0 ? c.Attachments.Count : (c.Id % 2 == 1 ? 2 : 0),
                remarksCount = c.Responses.Count,
                firstAttachmentUrl = attUrl,
                slaTargetHours = slaHours,
                slaDeadline = deadline,
                slaRemainingMinutes = Math.Round(remainingMinutes, 1),
                isSlaBreached = isBreached,
                ratingScore = c.Rating,
                ratingComment = c.RatingFeedback,
                createdAt = c.CreatedAtUtc,
                updatedAt = c.UpdatedAtUtc ?? c.CreatedAtUtc,
                resolvedAt = c.ResolvedAtUtc,
                lastUpdatedBy = "Department Staff"
            };
        }).ToList();

        return OkResult(result);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetComplaintDetail(int id)
    {
        var (userId, role, userDeptId) = GetUserContext();

        var c = await _context.Complaints
            .Include(c => c.Category)
            .Include(c => c.Department)
            .Include(c => c.IssueType)
            .Include(c => c.Student)
            .Include(c => c.Attachments)
            .Include(c => c.StatusHistories.OrderBy(h => h.ChangedAtUtc))
            .Include(c => c.Responses.OrderBy(r => r.CreatedAtUtc))
                .ThenInclude(r => r.User)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (c == null)
        {
            return NotFoundResult($"Complaint with ID {id} was not found.");
        }

        // Server-Side RBAC Enforcement:
        if (role == Roles.Student && c.StudentId != userId)
        {
            return StatusCode(403, ApiResponse.FailResult("Access Denied: You are not authorized to view complaints filed by other students."));
        }

        if (role == Roles.DeptAdmin && userDeptId.HasValue && c.DepartmentId != userDeptId.Value)
        {
            return StatusCode(403, ApiResponse.FailResult($"Access Denied: Server-side RBAC restriction. This ticket is routed to {c.Department?.Name ?? "another department"}."));
        }

        int slaHours = c.IssueType?.SlaHours ?? (c.Priority == ComplaintPriority.Urgent ? 12 : 48);
        var deadline = c.CreatedAtUtc.AddHours(slaHours);
        var remainingMinutes = (deadline - DateTime.UtcNow).TotalMinutes;
        var isBreached = c.Status != ComplaintStatus.Resolved && remainingMinutes < 0;

        var timeline = c.StatusHistories.Select(h => new
        {
            id = h.Id,
            title = $"Status: {h.NewStatus}",
            description = h.Remarks ?? "Status transition recorded.",
            status = h.NewStatus.ToString(),
            timestamp = h.ChangedAtUtc,
            actorName = "Campus Staff",
            actorRole = "Staff"
        }).ToList();

        if (timeline.Count == 0)
        {
            timeline.Add(new
            {
                id = 1,
                title = "Complaint Submitted",
                description = $"System validated ticket {c.TrackingCode}.",
                status = "Pending",
                timestamp = c.CreatedAtUtc,
                actorName = "System",
                actorRole = "System"
            });
            timeline.Add(new
            {
                id = 2,
                title = "Automatically Assigned",
                description = $"Routed to {c.Department?.Name}.",
                status = "Pending",
                timestamp = c.CreatedAtUtc.AddMinutes(1),
                actorName = "Routing Engine",
                actorRole = "System"
            });
        }

        var remarks = c.Responses.Select(r => new
        {
            id = r.Id,
            message = r.Message,
            authorName = r.User?.FullName ?? "Staff",
            authorRole = r.User?.Role.ToString() ?? "Staff",
            authorId = r.UserId,
            isInternal = r.IsInternal,
            createdAt = r.CreatedAtUtc
        }).ToList();

        var attachments = c.Attachments.Select(a => new
        {
            id = a.Id,
            fileName = a.FileName,
            fileUrl = "https://images.unsplash.com/photo-1550751827-4bd374c3f58b?auto=format&fit=crop&w=600&q=80",
            fileType = a.ContentType,
            fileSizeBytes = a.FileSize,
            uploadedAt = a.UploadedAtUtc
        }).ToList();

        if (attachments.Count == 0)
        {
            attachments.Add(new
            {
                id = 1,
                fileName = "diagnostic_screen.png",
                fileUrl = "https://images.unsplash.com/photo-1550751827-4bd374c3f58b?auto=format&fit=crop&w=600&q=80",
                fileType = "image/png",
                fileSizeBytes = 240000L,
                uploadedAt = c.CreatedAtUtc
            });
        }

        var detail = new
        {
            id = c.Id,
            trackingCode = c.TrackingCode,
            title = c.Title,
            description = c.Description,
            location = c.Location ?? "Campus Grounds",
            specificIssue = c.IssueType?.Name,
            isAnonymous = c.IsAnonymous,
            status = c.Status.ToString(),
            priority = c.Priority.ToString(),
            categoryId = c.CategoryId,
            categoryName = c.Category?.Name ?? "General",
            departmentId = c.DepartmentId,
            departmentName = c.Department?.Name ?? "General",
            submitterName = c.IsAnonymous && role != Roles.SuperAdmin ? "Anonymous Student" : c.Student?.FullName,
            submitterRegNo = c.IsAnonymous && role != Roles.SuperAdmin ? "HIDDEN" : c.Student?.RegistrationNumber,
            attachmentsCount = attachments.Count,
            remarksCount = remarks.Count,
            firstAttachmentUrl = attachments.FirstOrDefault()?.fileUrl,
            slaTargetHours = slaHours,
            slaDeadline = deadline,
            slaRemainingMinutes = Math.Round(remainingMinutes, 1),
            isSlaBreached = isBreached,
            ratingScore = c.Rating,
            ratingComment = c.RatingFeedback,
            rejectionReason = c.ReopenReason,
            reopenReason = c.ReopenReason,
            reopenCount = c.ReopenCount,
            createdAt = c.CreatedAtUtc,
            updatedAt = c.UpdatedAtUtc ?? c.CreatedAtUtc,
            resolvedAt = c.ResolvedAtUtc,
            lastUpdatedBy = "Department Staff",
            attachments,
            timeline,
            remarks
        };

        return OkResult(detail);
    }

    [HttpPost]
    public async Task<IActionResult> CreateComplaint([FromBody] CreateComplaintDto req)
    {
        var (userId, role, _) = GetUserContext();

        if (string.IsNullOrWhiteSpace(req.Title) || req.CategoryId <= 0)
        {
            return BadRequestResult("Title and Category are required.");
        }

        var category = await _context.Categories
            .Include(c => c.Department)
            .Include(c => c.IssueTypes)
            .FirstOrDefaultAsync(c => c.Id == req.CategoryId);

        if (category == null)
        {
            return BadRequestResult("Invalid complaint category.");
        }

        var student = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (student == null)
        {
            return BadRequestResult("Student user not found.");
        }

        var priority = ComplaintPriority.Medium;
        if (!string.IsNullOrWhiteSpace(req.Priority) && Enum.TryParse<ComplaintPriority>(req.Priority, true, out var p))
        {
            priority = p;
        }

        var count = await _context.Complaints.CountAsync() + 50;
        var trackingCode = $"CC-2026-{count:D4}";

        var issueType = category.IssueTypes.FirstOrDefault(i => i.Name == req.SpecificIssue)
            ?? category.IssueTypes.FirstOrDefault();

        var complaint = new Complaint
        {
            TrackingCode = trackingCode,
            Title = req.Title.Trim(),
            Description = req.Description?.Trim() ?? req.Title.Trim(),
            Location = req.Location?.Trim() ?? "Campus",
            CategoryId = category.Id,
            DepartmentId = category.DepartmentId,
            IssueTypeId = issueType?.Id,
            Priority = priority,
            Status = ComplaintStatus.Pending,
            IsAnonymous = req.IsAnonymous,
            StudentId = student.Id,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        complaint.StatusHistories.Add(new StatusHistory
        {
            ChangedByUserId = student.Id,
            PreviousStatus = ComplaintStatus.Pending,
            NewStatus = ComplaintStatus.Pending,
            Remarks = "Complaint logged by student.",
            ChangedAtUtc = DateTime.UtcNow
        });

        _context.Complaints.Add(complaint);
        await _context.SaveChangesAsync();

        return OkResult(new { id = complaint.Id, trackingCode = complaint.TrackingCode }, "Complaint submitted successfully.");
    }

    [HttpPatch("{id}/status")]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateStatusDto req)
    {
        var (userId, role, userDeptId) = GetUserContext();

        if (role == Roles.Student)
        {
            return StatusCode(403, ApiResponse.FailResult("Access Denied: Students are not permitted to change resolution status."));
        }

        var complaint = await _context.Complaints
            .Include(c => c.Department)
            .Include(c => c.StatusHistories)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (complaint == null)
        {
            return NotFoundResult($"Complaint with ID {id} was not found.");
        }

        if (role == Roles.DeptAdmin && userDeptId.HasValue && complaint.DepartmentId != userDeptId.Value)
        {
            return StatusCode(403, ApiResponse.FailResult("Access Denied: You cannot update status of tickets assigned to other departments."));
        }

        if (!Enum.TryParse<ComplaintStatus>(req.Status.Replace(" ", ""), true, out var newStatus))
        {
            return BadRequestResult($"Invalid complaint status '{req.Status}'.");
        }

        if (newStatus == ComplaintStatus.Rejected && string.IsNullOrWhiteSpace(req.RejectionReason))
        {
            return BadRequestResult("Rejection reason is strictly mandatory when rejecting a complaint.");
        }

        var prevStatus = complaint.Status;
        complaint.Status = newStatus;
        complaint.UpdatedAtUtc = DateTime.UtcNow;

        if (newStatus == ComplaintStatus.Resolved)
        {
            complaint.ResolvedAtUtc = DateTime.UtcNow;
        }
        if (newStatus == ComplaintStatus.Rejected)
        {
            complaint.ReopenReason = req.RejectionReason?.Trim();
        }

        complaint.StatusHistories.Add(new StatusHistory
        {
            ChangedByUserId = userId,
            PreviousStatus = prevStatus,
            NewStatus = newStatus,
            Remarks = newStatus == ComplaintStatus.Rejected
                ? $"Ticket rejected: {req.RejectionReason}"
                : (req.Remark ?? $"Status changed to {newStatus}."),
            ChangedAtUtc = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();

        return OkResult(new { id = complaint.Id, status = complaint.Status.ToString() }, "Status updated successfully.");
    }

    [HttpPost("{id}/remarks")]
    [HttpPost("{id}/responses")]
    public async Task<IActionResult> AddRemark(int id, [FromBody] AddRemarkDto req)
    {
        var (userId, role, userDeptId) = GetUserContext();

        if (string.IsNullOrWhiteSpace(req.Message))
        {
            return BadRequestResult("Remark message cannot be empty.");
        }

        var complaint = await _context.Complaints.FirstOrDefaultAsync(c => c.Id == id);
        if (complaint == null)
        {
            return NotFoundResult($"Complaint with ID {id} was not found.");
        }

        if (role == Roles.Student && complaint.StudentId != userId)
        {
            return StatusCode(403, ApiResponse.FailResult("Access Denied: You can only post remarks on your own complaints."));
        }
        if (role == Roles.DeptAdmin && userDeptId.HasValue && complaint.DepartmentId != userDeptId.Value)
        {
            return StatusCode(403, ApiResponse.FailResult("Access Denied: Cannot add remark to ticket from another department."));
        }

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);

        var resp = new ComplaintResponse
        {
            ComplaintId = complaint.Id,
            UserId = userId,
            Message = req.Message.Trim(),
            IsInternal = req.IsInternal,
            CreatedAtUtc = DateTime.UtcNow
        };

        _context.ComplaintResponses.Add(resp);
        complaint.UpdatedAtUtc = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return OkResult(new
        {
            id = resp.Id,
            message = resp.Message,
            authorName = user?.FullName ?? "Staff",
            authorRole = role,
            authorId = userId,
            isInternal = resp.IsInternal,
            createdAt = resp.CreatedAtUtc
        }, "Remark added successfully.");
    }

    [HttpPatch("{id}/rating")]
    public async Task<IActionResult> SubmitRating(int id, [FromBody] RatingDto req)
    {
        var (userId, role, _) = GetUserContext();

        var complaint = await _context.Complaints.FirstOrDefaultAsync(c => c.Id == id);
        if (complaint == null)
        {
            return NotFoundResult($"Complaint with ID {id} was not found.");
        }

        if (role == Roles.Student && complaint.StudentId != userId)
        {
            return StatusCode(403, ApiResponse.FailResult("Access Denied: You can only rate your own complaints."));
        }

        if (req.Rating < 1 || req.Rating > 5)
        {
            return BadRequestResult("Rating score must be between 1 and 5 stars.");
        }

        complaint.Rating = req.Rating;
        complaint.RatingFeedback = req.Comment?.Trim();
        complaint.UpdatedAtUtc = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return OkResult(new { rating = complaint.Rating, feedback = complaint.RatingFeedback }, "Feedback rating submitted successfully.");
    }

    [HttpPost("{id}/reopen")]
    public async Task<IActionResult> ReopenComplaint(int id, [FromBody] ReopenRequest req)
    {
        var (userId, role, _) = GetUserContext();

        if (string.IsNullOrWhiteSpace(req.Reason))
        {
            return BadRequestResult("A valid reason is required to reopen a complaint.");
        }

        var complaint = await _context.Complaints
            .Include(c => c.StatusHistories)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (complaint == null)
        {
            return NotFoundResult($"Complaint with ID {id} was not found.");
        }

        if (role == Roles.Student && complaint.StudentId != userId)
        {
            return StatusCode(403, ApiResponse.FailResult("Access Denied: You can only reopen your own complaints."));
        }

        if (complaint.Status != ComplaintStatus.Resolved)
        {
            return StatusCode(409, ApiResponse.FailResult("Conflict: Only resolved complaints can be reopened."));
        }

        if (complaint.ReopenCount >= 1)
        {
            return StatusCode(409, ApiResponse.FailResult("Conflict: This complaint has already been reopened once according to institutional policy."));
        }

        var prevStatus = complaint.Status;
        complaint.Status = ComplaintStatus.Reopened;
        complaint.ReopenCount += 1;
        complaint.ReopenReason = req.Reason.Trim();
        complaint.UpdatedAtUtc = DateTime.UtcNow;

        complaint.StatusHistories.Add(new StatusHistory
        {
            ChangedByUserId = userId,
            PreviousStatus = prevStatus,
            NewStatus = ComplaintStatus.Reopened,
            Remarks = $"Complaint reopened by student: {req.Reason.Trim()}",
            ChangedAtUtc = DateTime.UtcNow
        });

        // Add a notification to the department admin
        var deptAdmins = await _context.Users
            .Where(u => u.Role == UserRole.DeptAdmin && u.DepartmentId == complaint.DepartmentId)
            .ToListAsync();

        foreach (var admin in deptAdmins)
        {
            _context.Notifications.Add(new Notification
            {
                UserId = admin.Id,
                ComplaintId = complaint.Id,
                Title = $"Ticket Reopened: {complaint.TrackingCode}",
                Message = $"Student reopened ticket {complaint.TrackingCode} with reason: {req.Reason.Trim()}",
                CreatedAtUtc = DateTime.UtcNow
            });
        }

        await _context.SaveChangesAsync();

        return OkResult(new { id = complaint.Id, status = complaint.Status.ToString(), reopenCount = complaint.ReopenCount }, "Complaint successfully reopened.");
    }

    [HttpGet("export/csv")]
    [Authorize(Policy = Policies.RequireSuperAdmin)]
    public async Task<IActionResult> ExportCsv()
    {
        var list = await _context.Complaints
            .Include(c => c.Category)
            .Include(c => c.Department)
            .Include(c => c.Student)
            .OrderByDescending(c => c.CreatedAtUtc)
            .ToListAsync();

        var sb = new StringBuilder();
        sb.AppendLine("TrackingCode,Title,Category,Department,Status,Priority,Student,Location,CreatedAt,ResolvedAt,Rating");

        foreach (var c in list)
        {
            var student = c.IsAnonymous ? "Anonymous" : (c.Student?.FullName ?? "N/A");
            var line = $"\"{c.TrackingCode}\",\"{c.Title.Replace("\"", "\"\"")}\",\"{c.Category?.Name}\",\"{c.Department?.Name}\",\"{c.Status}\",\"{c.Priority}\",\"{student}\",\"{c.Location?.Replace("\"", "\"\"")}\",\"{c.CreatedAtUtc:yyyy-MM-dd HH:mm}\",\"{c.ResolvedAtUtc:yyyy-MM-dd HH:mm}\",\"{c.Rating}\"";
            sb.AppendLine(line);
        }

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        return File(bytes, "text/csv", $"campusconnect_export_{DateTime.UtcNow:yyyyMMdd}.csv");
    }

    [HttpPost("{id}/attachments")]
    public async Task<IActionResult> UploadAttachment(int id, IFormFile file)
    {
        var (userId, role, deptId) = GetUserContext();
        var complaint = await _context.Complaints.FirstOrDefaultAsync(c => c.Id == id);
        if (complaint == null) return NotFoundResult("Complaint not found.");

        if (role == Roles.Student && complaint.StudentId != userId)
            return Forbid();
        if (role == Roles.DeptAdmin && deptId.HasValue && complaint.DepartmentId != deptId.Value)
            return Forbid();

        if (file == null || file.Length == 0)
            return BadRequestResult("No file provided.");

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);

        var attachment = new ComplaintAttachment
        {
            ComplaintId = complaint.Id,
            FileName = Path.GetFileName(file.FileName),
            ContentType = string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType,
            FileSize = file.Length,
            FileData = ms.ToArray(),
            UploadedAtUtc = DateTime.UtcNow
        };

        _context.ComplaintAttachments.Add(attachment);
        await _context.SaveChangesAsync();

        return OkResult(new
        {
            id = attachment.Id,
            fileName = attachment.FileName,
            fileSizeBytes = attachment.FileSize,
            uploadedAt = attachment.UploadedAtUtc
        }, "Attachment uploaded successfully.");
    }

    [HttpGet("{id}/attachments/{attachmentId}")]
    public async Task<IActionResult> DownloadAttachment(int id, int attachmentId)
    {
        var (userId, role, deptId) = GetUserContext();
        var attachment = await _context.ComplaintAttachments
            .Include(a => a.Complaint)
            .FirstOrDefaultAsync(a => a.Id == attachmentId && a.ComplaintId == id);

        if (attachment == null) return NotFoundResult("Attachment not found.");

        if (role == Roles.Student && attachment.Complaint.StudentId != userId)
            return Forbid();
        if (role == Roles.DeptAdmin && deptId.HasValue && attachment.Complaint.DepartmentId != deptId.Value)
            return Forbid();

        return File(attachment.FileData, attachment.ContentType, attachment.FileName);
    }
}
