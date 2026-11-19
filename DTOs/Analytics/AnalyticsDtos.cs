namespace CompetitionApi.DTOs.Analytics;

public class AnalyticsSummaryDto
{
    public int TotalComplaints { get; set; }
    public int TotalRaised => TotalComplaints;
    public int PendingCount { get; set; }
    public int Pending => PendingCount;
    public int InProgressCount { get; set; }
    public int InProgress => InProgressCount;
    public int ResolvedCount { get; set; }
    public int Resolved => ResolvedCount;
    public int RejectedCount { get; set; }
    public int Rejected => RejectedCount;
    public int SlaBreachCount { get; set; }
    public int SlaComplianceRate { get; set; }
    public List<DepartmentStatDto> DepartmentBreakdown { get; set; } = new();
    public double ResolutionRate { get; set; }
    public double AvgResolutionHours { get; set; }
}

public class DepartmentStatDto
{
    public int DepartmentId { get; set; }
    public string DepartmentName { get; set; } = string.Empty;
    public string DepartmentCode { get; set; } = string.Empty;
    public int TotalComplaints { get; set; }
    public int Pending { get; set; }
    public int InProgress { get; set; }
    public int Resolved { get; set; }
    public int Rejected { get; set; }
    public double AvgResolutionHours { get; set; }
    public double SlaBreachRate { get; set; }
}

public class TrendPointDto
{
    public string DateLabel { get; set; } = string.Empty;
    public int TotalSubmitted { get; set; }
    public int ResolvedCount { get; set; }
}

public class ResolutionTimeDto
{
    public string DepartmentName { get; set; } = string.Empty;
    public double AvgHours { get; set; }
}

public class TopIssueDto
{
    public string IssueTypeName { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public int Count { get; set; }
}
