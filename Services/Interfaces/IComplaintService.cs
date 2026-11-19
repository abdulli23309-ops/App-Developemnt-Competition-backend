using System.Security.Claims;
using CompetitionApi.Authorization;
using CompetitionApi.DTOs.Complaints;
using Microsoft.AspNetCore.Http;

namespace CompetitionApi.Services.Interfaces;

public interface IComplaintService
{
    /// <summary>Returns complaints scoped to the caller's role/department.</summary>
    Task<List<ComplaintDto>> GetComplaintsAsync(ClaimsPrincipal user, string? search, string? status, int? departmentId);

    /// <summary>Returns a single complaint if the caller is authorized to see it.</summary>
    Task<ComplaintDetailDto?> GetComplaintAsync(int id, ClaimsPrincipal user);

    /// <summary>Creates a complaint for the given student.</summary>
    Task<ComplaintDto> CreateComplaintAsync(CreateComplaintRequest request, int studentId);

    /// <summary>Transitions status with business-rule validation (409 on invalid).</summary>
    Task<ComplaintDetailDto> UpdateStatusAsync(int id, UpdateStatusRequest request, int changedByUserId, string changedByRole, int? changedByDepartmentId);

    /// <summary>Updates priority.</summary>
    Task UpdatePriorityAsync(int id, string priority, int changedByUserId);

    /// <summary>Adds a response/remark to a complaint.</summary>
    Task<ComplaintDetailDto> AddResponseAsync(int id, AddResponseRequest request, int userId, string userRole);

    /// <summary>Submits a student rating (only on resolved complaints).</summary>
    Task SubmitRatingAsync(int id, RatingRequest request, int studentId);

    /// <summary>Reopens a resolved complaint (student, once only).</summary>
    Task<ComplaintDetailDto> ReopenAsync(int id, ReopenRequest request, int studentId);

    /// <summary>Returns attachment file data if authorized; null if not found/out of scope.</summary>
    Task<(byte[] FileData, string FileName, string ContentType)?> GetAttachmentAsync(int complaintId, int attachmentId, ClaimsPrincipal user);

    /// <summary>Exports complaints to CSV, scoped to caller.</summary>
    Task<string> ExportCsvAsync(ClaimsPrincipal user);
}