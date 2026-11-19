using CompetitionApi.Models.Enums;

namespace CompetitionApi.Models.Entities;

public class User
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? RegistrationNumber { get; set; }
    public string? AcademicProgram { get; set; }
    public string PasswordHash { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public int? DepartmentId { get; set; }
    public Department? Department { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<Complaint> Complaints { get; set; } = new List<Complaint>();
    public ICollection<ComplaintResponse> ComplaintResponses { get; set; } = new List<ComplaintResponse>();
    public ICollection<StatusHistory> StatusHistories { get; set; } = new List<StatusHistory>();
    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
}
