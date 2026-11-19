using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CompetitionApi.Data;

/// <summary>
/// Design-time factory for scaffolding SQL Server migrations.
/// Usage: dotnet ef migrations add <Name> --context ApplicationDbContext --output-dir Migrations
/// </summary>
public class SqlServerDesignTimeDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        optionsBuilder.UseSqlServer(
            "Server=DESKTOP-UD649GB\\SQLEXPRESS;Database=CompetitionDb;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True",
            b => b.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName));

        return new ApplicationDbContext(optionsBuilder.Options);
    }
}
