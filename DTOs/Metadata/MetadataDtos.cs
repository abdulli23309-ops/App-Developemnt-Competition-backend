namespace CompetitionApi.DTOs.Metadata;

public class CategoryDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int DepartmentId { get; set; }
    public string DepartmentName { get; set; } = string.Empty;
    public string DepartmentCode { get; set; } = string.Empty;
    public List<IssueTypeDto> IssueTypes { get; set; } = new();
}

public class IssueTypeDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string DefaultPriority { get; set; } = string.Empty;
    public int SlaHours { get; set; }
}

public class DepartmentDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
}
