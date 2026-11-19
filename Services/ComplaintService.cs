using System.Security.Claims;
using System.Text;
using CompetitionApi.Authorization;
using CompetitionApi.Data;
using CompetitionApi.DTOs.Complaints;
using CompetitionApi.Models.Entities;
using CompetitionApi.Models.Enums;
using CompetitionApi.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace CompetitionApi.Services;

public class ComplaintService : IComplaintService
{
    private readonly ApplicationDbContext _db;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ComplaintService(ApplicationDbContext db, IHttpContextAccessor httpContextAccessor)
    {
        _db = db;
        _httpContextAccessor = httpContextAccessor;
    }

    // --- Centralized scoped IQueryable ---
    // Student: own complaints only.
    // DeptAdmin: complaints in their department only.
    // SuperAdmin: all complaints.
    private IQueryable<Complaint> ApplyScope(IQueryable<Complaint> query, ClaimsPrincipal user)
    {
        var role = user.GetRole();
        var deptId = user.GetDepartmentId();
        var userId = user.GetUserId();

        return role switch
        {
            Roles.SuperAdmin => query,
            Roles.DeptAdmin => query.Where(c => deptId.HasValue && c.DepartmentId == deptId.Value),
            _ => query.Where(c => userId.HasValue && c.StudentId == userId.Value),
        };
    }

    public async Task<List<ComplaintDto>> GetComplaintsAsync(ClaimsPrincipal user, string? search, string? status, int? departmentId)
    {
        var query = _db.Complaints
            .Include(c => c.Category).ThenInclude(cat => cat.Department)
            .Include(c => c.IssueType)
            .Include(c => c.Student)
            .Include(c => c.Responses)
            .Include(c => c.Attachments)
            .AsQueryable();

        query = ApplyScope(query, user);

        // DeptAdmin filter by explicit departmentId param only if it matches their dept; SuperAdmin can filter.
        if (departmentId.HasValue)
        {
            var role = user.GetRole();
            if (role == Roles.SuperAdmin || role == Roles.DeptAdmin)
                query = query.Where(c => c.DepartmentId == departmentId.Value);
            else if (user.GetUserId().HasValue)
                query = query.Where(c => c.StudentId == user.GetUserId()!.Value && c.DepartmentId == departmentId.Value);
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

        if (!string.IsNullOrWhiteSpace(status) && !string.Equals(status, "all", StringComparison.OrdinalIgnoreCase))
        {
            var st = status.Trim().Replace(" ", "");
            if (Enum.TryParse<ComplaintStatus>(st, true, out var parsedStatus))
            {
                query = query.Where(c => c.Status == parsedStatus);
            }
        }

        var list = await query
            .OrderByDescending(c => c.CreatedAtUtc)
            .ToListAsync();

        return list.Select(c => MapToDto(c)).ToList();
    }

    public async Task<ComplaintDetailDto?> GetComplaintAsync(int id, ClaimsPrincipal user)
    {
        var complaint = await _db.Complaints
            .Include(c => c.Category).ThenInclude(cat => cat.Department)
            .Include(c => c.IssueType)
            .Include(c => c.Student)
            .Include(c => c.Attachments)
            .Include(c => c.Responses).ThenInclude(r => r.User)
            .Include(c => c.StatusHistories).ThenInclude(sh => sh.ChangedByUser)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (complaint == null) return null;

        // Authorization scope check
        var role = user.GetRole();
        var deptId = user.GetDepartmentId();
        var userId = user.GetUserId();
        bool visible = role switch
        {
            Roles.SuperAdmin => true,
            Roles.DeptAdmin => deptId.HasValue && complaint.DepartmentId == deptId.Value,
            _ => userId.HasValue && complaint.StudentId == userId.Value,
        };
        if (!visible) return null;

        return MapToDetailDto(complaint);
    }

    public async Task<ComplaintDto> CreateComplaintAsync(CreateComplaintRequest request, int studentId)
    {
        var student = await _db.Users.FindAsync(studentId);
        if (student == null) throw new InvalidOperationException("Student not found.");

        var category = await _db.Categories
            .Include(cat => cat.Department)
            .FirstOrDefaultAsync(cat => cat.Id == request.CategoryId);
        if (category == null) throw new InvalidOperationException("Category not found.");

        // Look up issue type by name within the category if SpecificIssue provided.
        IssueType? issueType = null;
        if (!string.IsNullOrWhiteSpace(request.SpecificIssue))
        {
            issueType = await _db.IssueTypes
                .FirstOrDefaultAsync(it => it.CategoryId == request.CategoryId &&
                    it.Name.ToLower() == request.SpecificIssue!.Trim().ToLower());
        }

        var trackingCode = GenerateTrackingCode();

        var priority = ComplaintPriority.Medium;
        if (!string.IsNullOrWhiteSpace(request.Priority) && Enum.TryParse<ComplaintPriority>(request.Priority, true, out var parsedP))
        {
            priority = parsedP;
        }

        var complaint = new Complaint
        {
            TrackingCode = trackingCode,
            Title = request.Title.Trim(),
            Description = request.Description?.Trim() ?? request.Title.Trim(),
            Location = request.Location?.Trim(),
            CategoryId = request.CategoryId,
            DepartmentId = category.DepartmentId,
            IssueTypeId = issueType?.Id,
            Priority = priority,
            Status = ComplaintStatus.Pending,
            IsAnonymous = request.IsAnonymous,
            StudentId = studentId,
            CreatedAtUtc = DateTime.UtcNow,
        };

        _db.Complaints.Add(complaint);
        await _db.SaveChangesAsync();

        // Initial status history entry
        _db.StatusHistories.Add(new StatusHistory
        {
            ComplaintId = complaint.Id,
            ChangedByUserId = studentId,
            PreviousStatus = ComplaintStatus.Pending,
            NewStatus = complaint.Status,
            Remarks = "Complaint submitted.",
            ChangedAtUtc = complaint.CreatedAtUtc,
        });

        // Notification to student
        _db.Notifications.Add(new Notification
        {
            UserId = studentId,
            ComplaintId = complaint.Id,
            Title = "Complaint Submitted",
            Message = $"Your complaint '{complaint.Title}' has been submitted. Tracking code: {complaint.TrackingCode}",
            IsRead = false,
            CreatedAtUtc = DateTime.UtcNow,
        });

        await _db.SaveChangesAsync();

        await _db.Entry(complaint).Reference(c => c.Category).LoadAsync();
        if (complaint.Category != null)
            await _db.Entry(complaint.Category).Reference(cat => cat.Department).LoadAsync();
        await _db.Entry(complaint).Reference(c => c.IssueType).LoadAsync();
        await _db.Entry(complaint).Reference(c => c.Student).LoadAsync();

        return MapToDto(complaint);
    }

    public async Task<ComplaintDetailDto> UpdateStatusAsync(int id, UpdateStatusRequest request, int changedByUserId, string changedByRole, int? changedByDepartmentId)
    {
        var complaint = await _db.Complaints
            .Include(c => c.Category).ThenInclude(cat => cat.Department)
            .Include(c => c.Student)
            .Include(c => c.Responses).ThenInclude(r => r.User)
            .Include(c => c.StatusHistories).ThenInclude(sh => sh.ChangedByUser)
            .Include(c => c.Attachments)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (complaint == null) throw new KeyNotFoundException("Complaint not found.");

        if (!string.Equals(changedByRole, Roles.Student, StringComparison.OrdinalIgnoreCase))
        {
            if (string.Equals(changedByRole, Roles.DeptAdmin, StringComparison.OrdinalIgnoreCase))
            {
                if (!changedByDepartmentId.HasValue || complaint.DepartmentId != changedByDepartmentId.Value)
                    throw new UnauthorizedAccessException("You can only update complaints in your department.");
            }
        }

        var previousStatus = complaint.Status.ToString();
        var newStatus = !string.IsNullOrWhiteSpace(request.Status)
            ? request.Status
            : (!string.IsNullOrWhiteSpace(request.NewStatus) ? request.NewStatus : previousStatus);

        ValidateTransition(previousStatus, newStatus, changedByRole, request);

        complaint.Status = Enum.Parse<ComplaintStatus>(newStatus, ignoreCase: true);
        complaint.UpdatedAtUtc = DateTime.UtcNow;
        if (complaint.Status == ComplaintStatus.Resolved)
            complaint.ResolvedAtUtc = DateTime.UtcNow;

        if (complaint.Status == ComplaintStatus.InProgress && previousStatus == nameof(ComplaintStatus.Resolved))
        {
            complaint.ReopenCount++;
            complaint.ReopenReason = request.RejectionReason ?? request.Remark;
        }

        _db.StatusHistories.Add(new StatusHistory
        {
            ComplaintId = complaint.Id,
            ChangedByUserId = changedByUserId,
            PreviousStatus = Enum.Parse<ComplaintStatus>(previousStatus),
            NewStatus = complaint.Status,
            Remarks = request.Remark ?? request.Remarks ?? $"Status changed to {newStatus}",
            ChangedAtUtc = DateTime.UtcNow,
        });

        _db.Notifications.Add(new Notification
        {
            UserId = complaint.StudentId,
            ComplaintId = complaint.Id,
            Title = "Status Updated",
            Message = $"Your complaint '{complaint.Title}' status changed from {previousStatus} to {newStatus}.",
            IsRead = false,
            CreatedAtUtc = DateTime.UtcNow,
        });

        await _db.SaveChangesAsync();

        await _db.Entry(complaint).Reference(c => c.Category).LoadAsync();
        if (complaint.Category != null)
            await _db.Entry(complaint.Category).Reference(cat => cat.Department).LoadAsync();
        await _db.Entry(complaint).Reference(c => c.IssueType).LoadAsync();
        return MapToDetailDto(complaint);
    }

    private void ValidateTransition(string previousStatus, string newStatus, string changedByRole, UpdateStatusRequest request)
    {
        bool isStudent = string.Equals(changedByRole, Roles.Student, StringComparison.OrdinalIgnoreCase);

        var valid = (previousStatus, newStatus) switch
        {
            ("Pending", "InProgress") => true,
            ("Pending", "Rejected") => true,
            ("InProgress", "Resolved") => true,
            ("InProgress", "Rejected") => true,
            ("Resolved", "InProgress") => isStudent,
            ("Resolved", "Reopened") => isStudent,
            _ => false,
        };

        if (!valid)
            throw new InvalidOperationException($"Invalid status transition from {previousStatus} to {newStatus}.");

        if (newStatus == nameof(ComplaintStatus.Rejected) && string.IsNullOrWhiteSpace(request.RejectionReason))
            throw new InvalidOperationException("Rejection requires a reason.");

        if (newStatus == nameof(ComplaintStatus.Resolved) && string.IsNullOrWhiteSpace(request.Remark) && string.IsNullOrWhiteSpace(request.Remarks))
            throw new InvalidOperationException("Resolution requires a remark.");

        if (previousStatus == nameof(ComplaintStatus.Resolved) && (newStatus == nameof(ComplaintStatus.InProgress) || newStatus == nameof(ComplaintStatus.Reopened))
            && string.IsNullOrWhiteSpace(request.RejectionReason) && string.IsNullOrWhiteSpace(request.Remark))
            throw new InvalidOperationException("Reopen requires a reason.");
    }

    public async Task UpdatePriorityAsync(int id, string priority, int changedByUserId)
    {
        var complaint = await _db.Complaints.FirstOrDefaultAsync(c => c.Id == id);
        if (complaint == null) throw new KeyNotFoundException("Complaint not found.");

        complaint.Priority = Enum.Parse<ComplaintPriority>(priority, ignoreCase: true);
        complaint.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    public async Task<ComplaintDetailDto> AddResponseAsync(int id, AddResponseRequest request, int userId, string userRole)
    {
        var complaint = await _db.Complaints
            .Include(c => c.Category).ThenInclude(cat => cat.Department)
            .Include(c => c.IssueType)
            .Include(c => c.Student)
            .Include(c => c.Responses).ThenInclude(r => r.User)
            .Include(c => c.StatusHistories).ThenInclude(sh => sh.ChangedByUser)
            .Include(c => c.Attachments)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (complaint == null) throw new KeyNotFoundException("Complaint not found.");

        var deptId = await _db.Users.Where(u => u.Id == userId).Select(u => u.DepartmentId).FirstOrDefaultAsync();
        bool visible = userRole switch
        {
            Roles.SuperAdmin => true,
            Roles.DeptAdmin => deptId.HasValue && complaint.DepartmentId == deptId.Value,
            _ => complaint.StudentId == userId,
        };
        if (!visible) throw new UnauthorizedAccessException("You do not have access to this complaint.");

        var response = new ComplaintResponse
        {
            ComplaintId = id,
            UserId = userId,
            Message = request.Message,
            IsInternal = request.IsInternal,
            CreatedAtUtc = DateTime.UtcNow,
        };
        _db.ComplaintResponses.Add(response);
        complaint.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return MapToDetailDto(complaint);
    }

    public async Task SubmitRatingAsync(int id, RatingRequest request, int studentId)
    {
        var complaint = await _db.Complaints.FirstOrDefaultAsync(c => c.Id == id && c.StudentId == studentId);
        if (complaint == null) throw new KeyNotFoundException("Complaint not found or not yours.");
        if (complaint.Status != ComplaintStatus.Resolved)
            throw new InvalidOperationException("Only resolved complaints can be rated.");
        if (complaint.Rating.HasValue)
            throw new InvalidOperationException("Complaint already rated.");

        complaint.Rating = request.Rating;
        complaint.RatingFeedback = request.Feedback;
        await _db.SaveChangesAsync();
    }

    public async Task<ComplaintDetailDto> ReopenAsync(int id, ReopenRequest request, int studentId)
    {
        var complaint = await _db.Complaints
            .Include(c => c.Category).ThenInclude(cat => cat.Department)
            .Include(c => c.IssueType)
            .Include(c => c.Student)
            .Include(c => c.Responses).ThenInclude(r => r.User)
            .Include(c => c.StatusHistories).ThenInclude(sh => sh.ChangedByUser)
            .Include(c => c.Attachments)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (complaint == null) throw new KeyNotFoundException("Complaint not found.");
        if (complaint.StudentId != studentId) throw new UnauthorizedAccessException("You do not have access to this complaint.");
        if (complaint.Status != ComplaintStatus.Resolved)
            throw new InvalidOperationException("Only resolved complaints can be reopened.");
        if (complaint.ReopenCount >= 1)
            throw new InvalidOperationException("A complaint can be reopened only once.");
        if (string.IsNullOrWhiteSpace(request.Reason))
            throw new InvalidOperationException("Reopen requires a reason.");

        var previousStatus = complaint.Status;
        complaint.Status = ComplaintStatus.Reopened;
        complaint.ReopenCount++;
        complaint.ReopenReason = request.Reason;
        complaint.UpdatedAtUtc = DateTime.UtcNow;

        _db.StatusHistories.Add(new StatusHistory
        {
            ComplaintId = complaint.Id,
            ChangedByUserId = studentId,
            PreviousStatus = previousStatus,
            NewStatus = complaint.Status,
            Remarks = $"Reopened by student: {request.Reason}",
            ChangedAtUtc = DateTime.UtcNow,
        });

        _db.Notifications.Add(new Notification
        {
            UserId = studentId,
            ComplaintId = complaint.Id,
            Title = "Complaint Reopened",
            Message = $"Your complaint '{complaint.Title}' has been reopened and is back in progress.",
            IsRead = false,
            CreatedAtUtc = DateTime.UtcNow,
        });

        await _db.SaveChangesAsync();
        return MapToDetailDto(complaint);
    }

    public async Task<(byte[] FileData, string FileName, string ContentType)?> GetAttachmentAsync(int complaintId, int attachmentId, ClaimsPrincipal user)
    {
        var attachment = await _db.ComplaintAttachments
            .Include(a => a.Complaint)
            .FirstOrDefaultAsync(a => a.Id == attachmentId && a.ComplaintId == complaintId);

        if (attachment == null) return null;

        var role = user.GetRole();
        var deptId = user.GetDepartmentId();
        var userId = user.GetUserId();
        bool visible = role switch
        {
            Roles.SuperAdmin => true,
            Roles.DeptAdmin => deptId.HasValue && attachment.Complaint.DepartmentId == deptId.Value,
            _ => userId.HasValue && attachment.Complaint.StudentId == userId.Value,
        };
        if (!visible) return null;

        return (attachment.FileData, attachment.FileName, attachment.ContentType);
    }

    public async Task<string> ExportCsvAsync(ClaimsPrincipal user)
    {
        var query = _db.Complaints
            .Include(c => c.Category).ThenInclude(cat => cat.Department)
            .Include(c => c.Student)
            .AsQueryable();
        query = ApplyScope(query, user);

        var rows = await query.OrderByDescending(c => c.CreatedAtUtc)
            .Select(c => new
            {
                c.TrackingCode,
                c.Title,
                Category = c.Category.Name,
                Department = c.Category.Department.Name,
                Status = c.Status.ToString(),
                Priority = c.Priority.ToString(),
                Student = c.Student.FullName,
                c.Student.RegistrationNumber,
                c.CreatedAtUtc,
            })
            .ToListAsync();

        var sb = new StringBuilder();
        sb.AppendLine("TrackingCode,Title,Category,Department,Status,Priority,Student,RegNo,CreatedAtUtc");
        foreach (var r in rows)
        {
            sb.AppendLine($"{Csv(r.TrackingCode)},{Csv(r.Title)},{Csv(r.Category)},{Csv(r.Department)},{Csv(r.Status)},{Csv(r.Priority)},{Csv(r.Student)},{Csv(r.RegistrationNumber)},{r.CreatedAtUtc}");
        }
        return sb.ToString();
    }

    private static string Csv(string? value) =>
        value == null ? "" : $"\"{value.Replace("\"", "\"\"")}\"";

    private string GenerateTrackingCode()
    {
        var year = DateTime.UtcNow.Year;
        var last = _db.Complaints
            .Where(c => c.TrackingCode.StartsWith($"CC-{year}-"))
            .OrderByDescending(c => c.TrackingCode)
            .Select(c => c.TrackingCode)
            .FirstOrDefault();
        var next = 1;
        if (last != null)
        {
            var suffix = last.Substring($"CC-{year}-".Length);
            if (int.TryParse(suffix, out var num)) next = num + 1;
        }
        return $"CC-{year}-{next:D4}";
    }

    private ComplaintDto MapToDto(Complaint c)
    {
        return new ComplaintDto
        {
            Id = c.Id,
            TrackingCode = c.TrackingCode,
            Title = c.Title,
            Description = c.Description,
            Location = c.Location,
            Status = c.Status.ToString(),
            Priority = c.Priority.ToString(),
            IsAnonymous = c.IsAnonymous,
            CategoryId = c.CategoryId,
            CategoryName = c.Category?.Name ?? "",
            DepartmentId = c.DepartmentId,
            DepartmentName = c.Category?.Department?.Name ?? c.Department?.Name ?? "",
            IssueTypeId = c.IssueTypeId,
            IssueTypeName = c.IssueType?.Name,
            StudentId = c.StudentId,
            StudentName = c.IsAnonymous ? null : c.Student?.FullName,
            StudentRegNo = c.IsAnonymous ? null : c.Student?.RegistrationNumber,
            Rating = c.Rating,
            RatingFeedback = c.RatingFeedback,
            ReopenCount = c.ReopenCount,
            ReopenReason = c.ReopenReason,
            CreatedAtUtc = c.CreatedAtUtc,
            UpdatedAtUtc = c.UpdatedAtUtc,
            ResolvedAtUtc = c.ResolvedAtUtc,
            RemarksCount = c.Responses?.Count ?? 0,
            AttachmentsCount = c.Attachments?.Count ?? 0,
            IsSlaBreached = false,
            SlaRemainingMinutes = 0,
        };
    }

    private ComplaintDetailDto MapToDetailDto(Complaint c)
    {
        return new ComplaintDetailDto
        {
            Id = c.Id,
            TrackingCode = c.TrackingCode,
            Title = c.Title,
            Description = c.Description,
            Location = c.Location,
            Status = c.Status.ToString(),
            Priority = c.Priority.ToString(),
            IsAnonymous = c.IsAnonymous,
            CategoryId = c.CategoryId,
            CategoryName = c.Category?.Name ?? "",
            DepartmentId = c.DepartmentId,
            DepartmentName = c.Category?.Department?.Name ?? c.Department?.Name ?? "",
            IssueTypeId = c.IssueTypeId,
            IssueTypeName = c.IssueType?.Name,
            StudentId = c.StudentId,
            SubmitterName = c.IsAnonymous ? "Anonymous Student" : (c.Student?.FullName ?? ""),
            SubmitterRegNo = c.IsAnonymous ? "HIDDEN" : c.Student?.RegistrationNumber,
            StudentName = c.IsAnonymous ? null : c.Student?.FullName,
            StudentRegNo = c.IsAnonymous ? null : c.Student?.RegistrationNumber,
            Rating = c.Rating,
            RatingFeedback = c.RatingFeedback,
            ReopenCount = c.ReopenCount,
            ReopenReason = c.ReopenReason,
            CreatedAtUtc = c.CreatedAtUtc,
            UpdatedAtUtc = c.UpdatedAtUtc,
            ResolvedAtUtc = c.ResolvedAtUtc,
            RemarksCount = c.Responses?.Count ?? 0,
            AttachmentsCount = c.Attachments?.Count ?? 0,
            LastUpdatedBy = c.Responses?.OrderByDescending(r => r.CreatedAtUtc).Select(r => r.User?.FullName).FirstOrDefault(),
            IsSlaBreached = false,
            SlaRemainingMinutes = 0,
            Attachments = c.Attachments?.Select(a => new AttachmentDto
            {
                Id = a.Id,
                FileName = a.FileName,
                ContentType = a.ContentType,
                FileType = a.ContentType,
                FileSize = a.FileSize,
                FileUrl = $"/api/complaints/{c.Id}/attachments/{a.Id}",
                UploadedAtUtc = a.UploadedAtUtc,
            }).ToList() ?? new List<AttachmentDto>(),
            Remarks = c.Responses?.Select(r => new ResponseDto
            {
                Id = r.Id,
                UserId = r.UserId,
                UserName = r.User?.FullName ?? "Staff",
                UserRole = r.User?.Role.ToString() ?? "Staff",
                Message = r.Message,
                IsInternal = r.IsInternal,
                CreatedAtUtc = r.CreatedAtUtc,
            }).ToList() ?? new List<ResponseDto>(),
            StatusHistories = c.StatusHistories?.Select(sh => new StatusHistoryDto
            {
                Id = sh.Id,
                ChangedByUserId = sh.ChangedByUserId,
                ChangedByName = sh.ChangedByUser?.FullName ?? "System",
                PreviousStatus = sh.PreviousStatus.ToString(),
                NewStatus = sh.NewStatus.ToString(),
                Remarks = sh.Remarks,
                ChangedAtUtc = sh.ChangedAtUtc,
            }).ToList() ?? new List<StatusHistoryDto>(),
            Timeline = c.StatusHistories?.Select((sh, i) => new TimelineEventDto
            {
                Id = i + 1,
                Title = $"Status changed to {sh.NewStatus}",
                Description = sh.Remarks ?? (sh.ChangedByUser != null ? $"Updated by {sh.ChangedByUser.FullName}" : "System update"),
                Timestamp = sh.ChangedAtUtc,
            }).ToList() ?? new List<TimelineEventDto>(),
        };
    }
}
