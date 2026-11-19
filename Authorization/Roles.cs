namespace CompetitionApi.Authorization;

public static class Roles
{
    public const string Student = "Student";
    public const string DeptAdmin = "DeptAdmin";
    public const string SuperAdmin = "SuperAdmin";
}

public static class Policies
{
    public const string RequireStudent = "RequireStudent";
    public const string RequireDeptAdmin = "RequireDeptAdmin";
    public const string RequireSuperAdmin = "RequireSuperAdmin";
}
