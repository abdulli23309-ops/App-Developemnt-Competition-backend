using CompetitionApi.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace CompetitionApi.Data;

/// <summary>
/// Abstract base DbContext defining all CampusConnect domain entities, DbSets, and relationships.
/// Concrete provider implementation is PostgreSqlApplicationDbContext.
/// </summary>
public abstract class ApplicationDbContext : DbContext
{
    protected ApplicationDbContext(DbContextOptions options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<IssueType> IssueTypes => Set<IssueType>();
    public DbSet<Complaint> Complaints => Set<Complaint>();
    public DbSet<ComplaintAttachment> ComplaintAttachments => Set<ComplaintAttachment>();
    public DbSet<ComplaintResponse> ComplaintResponses => Set<ComplaintResponse>();
    public DbSet<StatusHistory> StatusHistories => Set<StatusHistory>();
    public DbSet<Notification> Notifications => Set<Notification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}
