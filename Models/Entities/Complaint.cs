using CompetitionApi.Models.Enums;

namespace CompetitionApi.Models.Entities;

public class Complaint
{
    public int Id { get; set; }
    public string TrackingCode { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? Location { get; set; }

    public int CategoryId { get; set; }
    public Category Category { get; set; } = null!;

    public int DepartmentId { get; set; }
    public Department Department { get; set; } = null!;

    public int? IssueTypeId { get; set; }
    public IssueType? IssueType { get; set; }

    public ComplaintPriority Priority { get; set; } = ComplaintPriority.Medium;
    public ComplaintStatus Status { get; set; } = ComplaintStatus.Pending;
    public bool IsAnonymous { get; set; } = false;

    public int StudentId { get; set; }
    public User Student { get; set; } = null!;

    public int? Rating { get; set; }
    public string? RatingFeedback { get; set; }
    public int ReopenCount { get; set; } = 0;
    public string? ReopenReason { get; set; }

    public byte[] RowVersion { get; set; } = null!;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }
    public DateTime? ResolvedAtUtc { get; set; }

    public ICollection<ComplaintAttachment> Attachments { get; set; } = new List<ComplaintAttachment>();
    public ICollection<ComplaintResponse> Responses { get; set; } = new List<ComplaintResponse>();
    public ICollection<StatusHistory> StatusHistories { get; set; } = new List<StatusHistory>();
    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
}
