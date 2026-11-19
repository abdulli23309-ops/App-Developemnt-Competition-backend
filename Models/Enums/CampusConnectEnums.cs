namespace CompetitionApi.Models.Enums;

public enum UserRole
{
    Student = 1,
    DeptAdmin = 2,
    SuperAdmin = 3
}

public enum ComplaintStatus
{
    Pending = 1,
    InProgress = 2,
    Resolved = 3,
    Rejected = 4,
    Reopened = 5
}

public enum ComplaintPriority
{
    Low = 1,
    Medium = 2,
    High = 3,
    Urgent = 4
}
