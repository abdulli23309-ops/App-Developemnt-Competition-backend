namespace CompetitionApi.DTOs.Complaints;

public class CreateComplaintRequest
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? Location { get; set; }
    public int CategoryId { get; set; }
    public int? IssueTypeId { get; set; }
    public string? SpecificIssue { get; set; } // Issue type name, looked up within the category
    public string Priority { get; set; } = "Medium";
    public bool IsAnonymous { get; set; } = false;
    public List<string>? AttachmentUrls { get; set; } // Informational preview URLs from client
}

public class UpdateStatusRequest
{
    public string NewStatus { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty; // alias accepted by frontend
    public string? Remarks { get; set; }
    public string? Remark { get; set; } // alias accepted by frontend
    public string? RejectionReason { get; set; }
}

public class UpdatePriorityRequest
{
    public string Priority { get; set; } = string.Empty;
}

public class AddResponseRequest
{
    public string Message { get; set; } = string.Empty;
    public bool IsInternal { get; set; } = false;
}

public class RatingRequest
{
    public int Rating { get; set; }
    public string? Feedback { get; set; }
}

public class ReopenRequest
{
    public string Reason { get; set; } = string.Empty;
}

public class AttachmentDto
{
    public int Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public string FileType { get; set; } = string.Empty; // alias for frontend
    public long FileSize { get; set; }
    public string FileUrl { get; set; } = string.Empty; // download URL
    public DateTime UploadedAtUtc { get; set; }
}

public class StatusHistoryDto
{
    public int Id { get; set; }
    public int ChangedByUserId { get; set; }
    public string ChangedByName { get; set; } = string.Empty;
    public string PreviousStatus { get; set; } = string.Empty;
    public string NewStatus { get; set; } = string.Empty;
    public string? Remarks { get; set; }
    public DateTime ChangedAtUtc { get; set; }
}

public class ResponseDto
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string UserRole { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public bool IsInternal { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public class ComplaintSummaryDto
{
    public int Id { get; set; }
    public string TrackingCode { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? Location { get; set; }
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public int DepartmentId { get; set; }
    public string DepartmentName { get; set; } = string.Empty;
    public int? IssueTypeId { get; set; }
    public string? IssueTypeName { get; set; }
    public string Priority { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool IsAnonymous { get; set; }
    public int StudentId { get; set; }
    public string SubmitterName { get; set; } = string.Empty;
    public string? SubmitterRegNo { get; set; }
    public int? Rating { get; set; }
    public string? RatingFeedback { get; set; }
    public int ReopenCount { get; set; }
    public string? ReopenReason { get; set; }
    public int SlaHours { get; set; }
    public DateTime SlaDeadlineUtc { get; set; }
    public bool IsSlaBreached { get; set; }
    public int AttachmentsCount { get; set; }
    public int ResponsesCount { get; set; }
    public int RemarksCount { get; set; } // alias for responses count in list view
    public string? LastUpdatedBy { get; set; }
    public int SlaRemainingMinutes { get; set; }
    public List<TimelineEventDto> Timeline { get; set; } = new();
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public DateTime? ResolvedAtUtc { get; set; }
}

public class ComplaintDetailDto : ComplaintSummaryDto
{
    public string? StudentName { get => SubmitterName; set => SubmitterName = value ?? string.Empty; }
    public string? StudentRegNo { get => SubmitterRegNo; set => SubmitterRegNo = value; }
    public List<AttachmentDto> Attachments { get; set; } = new();
    public List<StatusHistoryDto> StatusHistories { get; set; } = new();
    public List<ResponseDto> Responses { get; set; } = new();
    public List<ResponseDto> Remarks { get => Responses; set => Responses = value; }
}

public class TimelineEventDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}

public class ComplaintDto
{
    public int Id { get; set; }
    public string TrackingCode { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? Location { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public bool IsAnonymous { get; set; }
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public int DepartmentId { get; set; }
    public string DepartmentName { get; set; } = string.Empty;
    public int? IssueTypeId { get; set; }
    public string? IssueTypeName { get; set; }
    public int StudentId { get; set; }
    public string? StudentName { get; set; }
    public string? StudentRegNo { get; set; }
    public int? Rating { get; set; }
    public string? RatingFeedback { get; set; }
    public int ReopenCount { get; set; }
    public string? ReopenReason { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public DateTime? ResolvedAtUtc { get; set; }
    public int RemarksCount { get; set; }
    public int AttachmentsCount { get; set; }
    public string? LastUpdatedBy { get; set; }
    public bool IsSlaBreached { get; set; }
    public int SlaRemainingMinutes { get; set; }
    public List<TimelineEventDto> Timeline { get; set; } = new();
}
