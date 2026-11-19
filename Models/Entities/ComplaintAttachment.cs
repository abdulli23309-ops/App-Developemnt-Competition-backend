namespace CompetitionApi.Models.Entities;

public class ComplaintAttachment
{
    public int Id { get; set; }
    public int ComplaintId { get; set; }
    public Complaint Complaint { get; set; } = null!;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public byte[] FileData { get; set; } = null!;
    public DateTime UploadedAtUtc { get; set; } = DateTime.UtcNow;
}
