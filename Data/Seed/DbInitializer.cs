using CompetitionApi.Models.Entities;
using CompetitionApi.Models.Enums;
using Microsoft.AspNetCore.Identity;

namespace CompetitionApi.Data.Seed;

public static class DbInitializer
{
    public static void Initialize(PostgreSqlApplicationDbContext context)
    {
        var hasher = new PasswordHasher<User>();
        var defaultPasswordHash = hasher.HashPassword(new User(), "Password123!");

        // 1. Seed 4 Departments
        if (!context.Departments.Any())
        {
            var departments = new List<Department>
            {
                new()
                {
                    Name = "Cafeteria",
                    Code = "CAFE",
                    Description = "Dining halls, meal quality, hygiene, and campus nutrition.",
                    CreatedAtUtc = DateTime.UtcNow
                },
                new()
                {
                    Name = "Library",
                    Code = "LIB",
                    Description = "Central library, book banks, digital IEEE repository, and study spaces.",
                    CreatedAtUtc = DateTime.UtcNow
                },
                new()
                {
                    Name = "Laboratories & IT",
                    Code = "IT_LABS",
                    Description = "Computing labs, campus network, high-performance GPU workstations, and Wi-Fi.",
                    CreatedAtUtc = DateTime.UtcNow
                },
                new()
                {
                    Name = "Teaching & Academic",
                    Code = "ACADEMIC",
                    Description = "Lecture scheduling, course curriculum, examinations, and faculty consultation.",
                    CreatedAtUtc = DateTime.UtcNow
                }
            };

            context.Departments.AddRange(departments);
            context.SaveChanges();
        }

        var cafeDept = context.Departments.First(d => d.Code == "CAFE");
        var libDept = context.Departments.First(d => d.Code == "LIB");
        var itDept = context.Departments.First(d => d.Code == "IT_LABS");
        var acadDept = context.Departments.First(d => d.Code == "ACADEMIC");

        // 2. Seed 4 Categories (1 per department)
        if (!context.Categories.Any())
        {
            var categories = new List<Category>
            {
                new()
                {
                    Name = "Cafeteria & Food Services",
                    Description = "Cafeteria food hygiene, freshness, menu pricing, and service issues.",
                    DepartmentId = cafeDept.Id,
                    CreatedAtUtc = DateTime.UtcNow
                },
                new()
                {
                    Name = "Library & Learning Resources",
                    Description = "Book availability, digital IEEE portal access, quiet zones, and study desks.",
                    DepartmentId = libDept.Id,
                    CreatedAtUtc = DateTime.UtcNow
                },
                new()
                {
                    Name = "Laboratories & Campus IT",
                    Description = "Hardware faults, software licenses, campus Wi-Fi, and lab equipment.",
                    DepartmentId = itDept.Id,
                    CreatedAtUtc = DateTime.UtcNow
                },
                new()
                {
                    Name = "Teaching & Academic Affairs",
                    Description = "Class schedules, syllabus progress, evaluation fairness, and faculty office hours.",
                    DepartmentId = acadDept.Id,
                    CreatedAtUtc = DateTime.UtcNow
                }
            };

            context.Categories.AddRange(categories);
            context.SaveChanges();
        }

        var cafeCat = context.Categories.First(c => c.DepartmentId == cafeDept.Id);
        var libCat = context.Categories.First(c => c.DepartmentId == libDept.Id);
        var itCat = context.Categories.First(c => c.DepartmentId == itDept.Id);
        var acadCat = context.Categories.First(c => c.DepartmentId == acadDept.Id);

        // 3. Seed Exactly 16 Official Issue Types (4 per category)
        if (!context.IssueTypes.Any())
        {
            var issueTypes = new List<IssueType>
            {
                // Cafeteria (4)
                new() { Name = "Food Quality & Freshness", CategoryId = cafeCat.Id, DefaultPriority = ComplaintPriority.High, SlaHours = 24, CreatedAtUtc = DateTime.UtcNow },
                new() { Name = "Hygiene & Sanitation", CategoryId = cafeCat.Id, DefaultPriority = ComplaintPriority.Urgent, SlaHours = 12, CreatedAtUtc = DateTime.UtcNow },
                new() { Name = "Pricing & Overcharging", CategoryId = cafeCat.Id, DefaultPriority = ComplaintPriority.Medium, SlaHours = 48, CreatedAtUtc = DateTime.UtcNow },
                new() { Name = "Staff Behavior & Service Speed", CategoryId = cafeCat.Id, DefaultPriority = ComplaintPriority.Low, SlaHours = 72, CreatedAtUtc = DateTime.UtcNow },

                // Library (4)
                new() { Name = "Book Availability & Catalog Issue", CategoryId = libCat.Id, DefaultPriority = ComplaintPriority.Medium, SlaHours = 48, CreatedAtUtc = DateTime.UtcNow },
                new() { Name = "Digital Resources & Wi-Fi Access", CategoryId = libCat.Id, DefaultPriority = ComplaintPriority.High, SlaHours = 24, CreatedAtUtc = DateTime.UtcNow },
                new() { Name = "Noise Level & Study Environment", CategoryId = libCat.Id, DefaultPriority = ComplaintPriority.Low, SlaHours = 48, CreatedAtUtc = DateTime.UtcNow },
                new() { Name = "Air Conditioning & Lighting", CategoryId = libCat.Id, DefaultPriority = ComplaintPriority.Medium, SlaHours = 36, CreatedAtUtc = DateTime.UtcNow },

                // Laboratories & IT (4)
                new() { Name = "Hardware Malfunction & Workstations", CategoryId = itCat.Id, DefaultPriority = ComplaintPriority.High, SlaHours = 24, CreatedAtUtc = DateTime.UtcNow },
                new() { Name = "Software License & IDE Installation", CategoryId = itCat.Id, DefaultPriority = ComplaintPriority.Medium, SlaHours = 48, CreatedAtUtc = DateTime.UtcNow },
                new() { Name = "Campus Wi-Fi & Network Outage", CategoryId = itCat.Id, DefaultPriority = ComplaintPriority.Urgent, SlaHours = 12, CreatedAtUtc = DateTime.UtcNow },
                new() { Name = "Lab Projector & Audio System", CategoryId = itCat.Id, DefaultPriority = ComplaintPriority.Medium, SlaHours = 36, CreatedAtUtc = DateTime.UtcNow },

                // Teaching & Academic (4)
                new() { Name = "Lecture Scheduling & Timetable Clash", CategoryId = acadCat.Id, DefaultPriority = ComplaintPriority.High, SlaHours = 24, CreatedAtUtc = DateTime.UtcNow },
                new() { Name = "Course Material & Syllabus Coverage", CategoryId = acadCat.Id, DefaultPriority = ComplaintPriority.Medium, SlaHours = 48, CreatedAtUtc = DateTime.UtcNow },
                new() { Name = "Grading & Evaluation Query", CategoryId = acadCat.Id, DefaultPriority = ComplaintPriority.High, SlaHours = 48, CreatedAtUtc = DateTime.UtcNow },
                new() { Name = "Faculty Availability & Office Hours", CategoryId = acadCat.Id, DefaultPriority = ComplaintPriority.Low, SlaHours = 72, CreatedAtUtc = DateTime.UtcNow }
            };

            context.IssueTypes.AddRange(issueTypes);
            context.SaveChanges();
        }

        // 4. Seed 6 Official Demo Users
        if (!context.Users.Any())
        {
            var users = new List<User>
            {
                // 1. Student Demo User
                new()
                {
                    FullName = "Sarah Chen",
                    Email = "s.chen@student.biit.edu.pk",
                    RegistrationNumber = "BIIT-2023-SE-042",
                    AcademicProgram = "BS Software Engineering (Faculty of Computing)",
                    PasswordHash = defaultPasswordHash,
                    Role = UserRole.Student,
                    DepartmentId = null,
                    CreatedAtUtc = DateTime.UtcNow.AddMonths(-3)
                },
                // 2. Cafeteria Admin
                new()
                {
                    FullName = "Warden Asif",
                    Email = "cafeteria.admin@biit.edu.pk",
                    RegistrationNumber = "EMP-CAFE-01",
                    AcademicProgram = null,
                    PasswordHash = defaultPasswordHash,
                    Role = UserRole.DeptAdmin,
                    DepartmentId = cafeDept.Id,
                    CreatedAtUtc = DateTime.UtcNow.AddYears(-1)
                },
                // 3. Library Admin
                new()
                {
                    FullName = "Bushra R.",
                    Email = "library.admin@biit.edu.pk",
                    RegistrationNumber = "EMP-LIB-02",
                    AcademicProgram = null,
                    PasswordHash = defaultPasswordHash,
                    Role = UserRole.DeptAdmin,
                    DepartmentId = libDept.Id,
                    CreatedAtUtc = DateTime.UtcNow.AddYears(-1)
                },
                // 4. IT & Labs Admin
                new()
                {
                    FullName = "Engr. Farhan",
                    Email = "itlabs.admin@biit.edu.pk",
                    RegistrationNumber = "EMP-IT-03",
                    AcademicProgram = null,
                    PasswordHash = defaultPasswordHash,
                    Role = UserRole.DeptAdmin,
                    DepartmentId = itDept.Id,
                    CreatedAtUtc = DateTime.UtcNow.AddYears(-1)
                },
                // 5. Teaching & Academic Admin
                new()
                {
                    FullName = "Dr. Arshad",
                    Email = "academics.admin@biit.edu.pk",
                    RegistrationNumber = "EMP-ACAD-04",
                    AcademicProgram = null,
                    PasswordHash = defaultPasswordHash,
                    Role = UserRole.DeptAdmin,
                    DepartmentId = acadDept.Id,
                    CreatedAtUtc = DateTime.UtcNow.AddYears(-1)
                },
                // 6. Super Admin
                new()
                {
                    FullName = "Dean of Academics / Registrar Office",
                    Email = "superadmin@biit.edu.pk",
                    RegistrationNumber = "DIR-REG-001",
                    AcademicProgram = null,
                    PasswordHash = defaultPasswordHash,
                    Role = UserRole.SuperAdmin,
                    DepartmentId = null,
                    CreatedAtUtc = DateTime.UtcNow.AddYears(-2)
                }
            };

            context.Users.AddRange(users);
            context.SaveChanges();

            // 5. Seed Initial Demo Complaints for Sarah Chen to populate dashboard
            var student = context.Users.First(u => u.Email == "s.chen@student.biit.edu.pk");
            var itIssue = context.IssueTypes.First(i => i.Name == "Hardware Malfunction & Workstations");
            var cafeIssue = context.IssueTypes.First(i => i.Name == "Food Quality & Freshness");
            var libIssue = context.IssueTypes.First(i => i.Name == "Digital Resources & Wi-Fi Access");

            var complaints = new List<Complaint>
            {
                new()
                {
                    TrackingCode = "CC-2026-0001",
                    Title = "GPU Rig #4 in Lab 3 keeps crashing during CUDA training",
                    Description = "The high-end workstation in Lab 3 restarts automatically whenever CUDA kernel allocations exceed 4GB. Disrupting final year deep learning lab sessions.",
                    Location = "Academic Block B, CS Lab 3",
                    CategoryId = itCat.Id,
                    DepartmentId = itDept.Id,
                    IssueTypeId = itIssue.Id,
                    Priority = ComplaintPriority.High,
                    Status = ComplaintStatus.InProgress,
                    IsAnonymous = false,
                    StudentId = student.Id,
                    CreatedAtUtc = DateTime.UtcNow.AddDays(-2),
                    UpdatedAtUtc = DateTime.UtcNow.AddHours(-4)
                },
                new()
                {
                    TrackingCode = "CC-2026-0002",
                    Title = "Cafeteria fresh juice dispenser uncleaned and leaking",
                    Description = "The fresh orange juice dispenser near Counter B has sticky residue from yesterday and attracts flies. Urgent sanitation required.",
                    Location = "Main Student Cafeteria, Counter B",
                    CategoryId = cafeCat.Id,
                    DepartmentId = cafeDept.Id,
                    IssueTypeId = cafeIssue.Id,
                    Priority = ComplaintPriority.Urgent,
                    Status = ComplaintStatus.Pending,
                    IsAnonymous = false,
                    StudentId = student.Id,
                    CreatedAtUtc = DateTime.UtcNow.AddHours(-18),
                    UpdatedAtUtc = DateTime.UtcNow.AddHours(-18)
                },
                new()
                {
                    TrackingCode = "CC-2026-0003",
                    Title = "IEEE Xplore authentication gateway timed out",
                    Description = "Students attempting to access IEEE research papers from the library workstations are redirected to an expired session error.",
                    Location = "Central Library, Research Section",
                    CategoryId = libCat.Id,
                    DepartmentId = libDept.Id,
                    IssueTypeId = libIssue.Id,
                    Priority = ComplaintPriority.High,
                    Status = ComplaintStatus.Resolved,
                    IsAnonymous = false,
                    StudentId = student.Id,
                    Rating = 5,
                    RatingFeedback = "Resolved quickly by the librarian within 2 hours. Excellent service!",
                    CreatedAtUtc = DateTime.UtcNow.AddDays(-5),
                    UpdatedAtUtc = DateTime.UtcNow.AddDays(-4),
                    ResolvedAtUtc = DateTime.UtcNow.AddDays(-4)
                }
            };

            context.Complaints.AddRange(complaints);
            context.SaveChanges();

            // Seed initial status history and remarks for demo complaints
            var c1 = context.Complaints.First(c => c.TrackingCode == "CC-2026-0001");
            var c3 = context.Complaints.First(c => c.TrackingCode == "CC-2026-0003");
            var itAdminUser = context.Users.First(u => u.Email == "itlabs.admin@biit.edu.pk");
            var libAdminUser = context.Users.First(u => u.Email == "library.admin@biit.edu.pk");

            context.StatusHistories.AddRange(
                new StatusHistory
                {
                    ComplaintId = c1.Id,
                    ChangedByUserId = student.Id,
                    PreviousStatus = ComplaintStatus.Pending,
                    NewStatus = ComplaintStatus.Pending,
                    Remarks = "Complaint submitted by student.",
                    ChangedAtUtc = c1.CreatedAtUtc
                },
                new StatusHistory
                {
                    ComplaintId = c1.Id,
                    ChangedByUserId = itAdminUser.Id,
                    PreviousStatus = ComplaintStatus.Pending,
                    NewStatus = ComplaintStatus.InProgress,
                    Remarks = "Hardware diagnostic initiated on Workstation #4. Swapping thermal paste and PSU.",
                    ChangedAtUtc = DateTime.UtcNow.AddHours(-4)
                },
                new StatusHistory
                {
                    ComplaintId = c3.Id,
                    ChangedByUserId = libAdminUser.Id,
                    PreviousStatus = ComplaintStatus.InProgress,
                    NewStatus = ComplaintStatus.Resolved,
                    Remarks = "Proxy token renewed with IEEE publisher. Gateway operational.",
                    ChangedAtUtc = c3.ResolvedAtUtc ?? DateTime.UtcNow
                }
            );

            context.ComplaintResponses.AddRange(
                new ComplaintResponse
                {
                    ComplaintId = c1.Id,
                    UserId = itAdminUser.Id,
                    Message = "We have identified thermal throttling as the root cause. A replacement GPU fan has been ordered and will be fitted today.",
                    IsInternal = false,
                    CreatedAtUtc = DateTime.UtcNow.AddHours(-4)
                },
                new ComplaintResponse
                {
                    ComplaintId = c3.Id,
                    UserId = libAdminUser.Id,
                    Message = "The institutional IP whitelist was refreshed on the IEEE publisher portal. You can now login seamlessly.",
                    IsInternal = false,
                    CreatedAtUtc = c3.ResolvedAtUtc ?? DateTime.UtcNow
                }
            );

            context.Notifications.AddRange(
                new Notification
                {
                    UserId = student.Id,
                    ComplaintId = c1.Id,
                    Title = "Complaint Under Investigation",
                    Message = "Engr. Farhan moved your complaint CC-2026-0001 to In Progress.",
                    IsRead = false,
                    CreatedAtUtc = DateTime.UtcNow.AddHours(-4)
                },
                new Notification
                {
                    UserId = student.Id,
                    ComplaintId = c3.Id,
                    Title = "Complaint Resolved",
                    Message = "Your complaint CC-2026-0003 has been resolved by Central Library.",
                    IsRead = true,
                    CreatedAtUtc = c3.ResolvedAtUtc ?? DateTime.UtcNow
                }
            );

            context.SaveChanges();
        }
    }
}
