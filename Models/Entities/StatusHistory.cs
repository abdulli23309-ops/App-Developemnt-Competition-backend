using CompetitionApi.Models.Enums;

namespace CompetitionApi.Models.Entities;

public class StatusHistory
{
    public int Id { get; set; }
    public int ComplaintId { get; set; }
    public Complaint Complaint { get; set; } = null!;
    public int ChangedByUserId { get; set; }
    public User ChangedByUser { get; set; } = null!;
    public ComplaintStatus PreviousStatus { get; set; }
    public ComplaintStatus NewStatus { get; set; }
    public string? Remarks { get; set; }
    public DateTime ChangedAtUtc { get; set; } = DateTime.UtcNow;
}
