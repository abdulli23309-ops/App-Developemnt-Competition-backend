using CompetitionApi.Models.Enums;

namespace CompetitionApi.Models.Entities;

public class IssueType
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int CategoryId { get; set; }
    public Category Category { get; set; } = null!;
    public ComplaintPriority DefaultPriority { get; set; } = ComplaintPriority.Medium;
    public int SlaHours { get; set; } = 48;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<Complaint> Complaints { get; set; } = new List<Complaint>();
}
