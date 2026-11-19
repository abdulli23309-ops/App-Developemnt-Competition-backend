using CompetitionApi.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CompetitionApi.Data.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(u => u.Id);
        builder.Property(u => u.FullName).HasMaxLength(150).IsRequired();
        builder.Property(u => u.Email).HasMaxLength(150).IsRequired();
        builder.HasIndex(u => u.Email).IsUnique();
        builder.Property(u => u.RegistrationNumber).HasMaxLength(50);
        builder.Property(u => u.AcademicProgram).HasMaxLength(150);
        builder.Property(u => u.PasswordHash).IsRequired();
        builder.Property(u => u.Role).HasConversion<string>().HasMaxLength(30).IsRequired();

        builder.HasOne(u => u.Department)
            .WithMany(d => d.StaffUsers)
            .HasForeignKey(u => u.DepartmentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class DepartmentConfiguration : IEntityTypeConfiguration<Department>
{
    public void Configure(EntityTypeBuilder<Department> builder)
    {
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Name).HasMaxLength(100).IsRequired();
        builder.Property(d => d.Code).HasMaxLength(30).IsRequired();
        builder.HasIndex(d => d.Code).IsUnique();
        builder.Property(d => d.Description).HasMaxLength(500);
    }
}

public class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Name).HasMaxLength(100).IsRequired();
        builder.Property(c => c.Description).HasMaxLength(500);

        builder.HasOne(c => c.Department)
            .WithMany(d => d.Categories)
            .HasForeignKey(c => c.DepartmentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class IssueTypeConfiguration : IEntityTypeConfiguration<IssueType>
{
    public void Configure(EntityTypeBuilder<IssueType> builder)
    {
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Name).HasMaxLength(150).IsRequired();
        builder.Property(i => i.DefaultPriority).HasConversion<string>().HasMaxLength(30).IsRequired();

        builder.HasOne(i => i.Category)
            .WithMany(c => c.IssueTypes)
            .HasForeignKey(i => i.CategoryId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class ComplaintConfiguration : IEntityTypeConfiguration<Complaint>
{
    public void Configure(EntityTypeBuilder<Complaint> builder)
    {
        builder.HasKey(c => c.Id);
        builder.Property(c => c.TrackingCode).HasMaxLength(30).IsRequired();
        builder.HasIndex(c => c.TrackingCode).IsUnique();

        builder.Property(c => c.Title).HasMaxLength(200).IsRequired();
        builder.Property(c => c.Description).IsRequired();
        builder.Property(c => c.Location).HasMaxLength(150);

        builder.Property(c => c.Priority).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(c => c.Status).HasConversion<string>().HasMaxLength(30).IsRequired();

        builder.Property(c => c.RowVersion).IsRowVersion();

        builder.HasOne(c => c.Student)
            .WithMany(u => u.Complaints)
            .HasForeignKey(c => c.StudentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.Department)
            .WithMany(d => d.Complaints)
            .HasForeignKey(c => c.DepartmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.Category)
            .WithMany(cat => cat.Complaints)
            .HasForeignKey(c => c.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.IssueType)
            .WithMany(it => it.Complaints)
            .HasForeignKey(c => c.IssueTypeId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public class ComplaintAttachmentConfiguration : IEntityTypeConfiguration<ComplaintAttachment>
{
    public void Configure(EntityTypeBuilder<ComplaintAttachment> builder)
    {
        builder.HasKey(a => a.Id);
        builder.Property(a => a.FileName).HasMaxLength(255).IsRequired();
        builder.Property(a => a.ContentType).HasMaxLength(100).IsRequired();
        builder.Property(a => a.FileData).HasColumnType("varbinary(max)").IsRequired();

        builder.HasOne(a => a.Complaint)
            .WithMany(c => c.Attachments)
            .HasForeignKey(a => a.ComplaintId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class ComplaintResponseConfiguration : IEntityTypeConfiguration<ComplaintResponse>
{
    public void Configure(EntityTypeBuilder<ComplaintResponse> builder)
    {
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Message).IsRequired();

        builder.HasOne(r => r.Complaint)
            .WithMany(c => c.Responses)
            .HasForeignKey(r => r.ComplaintId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(r => r.User)
            .WithMany(u => u.ComplaintResponses)
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class StatusHistoryConfiguration : IEntityTypeConfiguration<StatusHistory>
{
    public void Configure(EntityTypeBuilder<StatusHistory> builder)
    {
        builder.HasKey(s => s.Id);
        builder.Property(s => s.PreviousStatus).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(s => s.NewStatus).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(s => s.Remarks).HasMaxLength(1000);

        builder.HasOne(s => s.Complaint)
            .WithMany(c => c.StatusHistories)
            .HasForeignKey(s => s.ComplaintId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(s => s.ChangedByUser)
            .WithMany(u => u.StatusHistories)
            .HasForeignKey(s => s.ChangedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.HasKey(n => n.Id);
        builder.Property(n => n.Title).HasMaxLength(150).IsRequired();
        builder.Property(n => n.Message).HasMaxLength(1000).IsRequired();

        builder.HasOne(n => n.User)
            .WithMany(u => u.Notifications)
            .HasForeignKey(n => n.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(n => n.Complaint)
            .WithMany(c => c.Notifications)
            .HasForeignKey(n => n.ComplaintId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
