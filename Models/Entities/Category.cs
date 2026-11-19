namespace CompetitionApi.Models.Entities;

public class Category
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int DepartmentId { get; set; }
    public Department Department { get; set; } = null!;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<IssueType> IssueTypes { get; set; } = new List<IssueType>();
    public ICollection<Complaint> Complaints { get; set; } = new List<Complaint>();
}
