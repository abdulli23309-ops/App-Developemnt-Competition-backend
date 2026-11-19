using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CompetitionApi.Data;

/// <summary>
/// Design-time factory for scaffolding PostgreSQL migrations.
/// Usage: dotnet ef migrations add <Name> --context PostgreSqlApplicationDbContext --output-dir Migrations/PostgreSql
/// </summary>
public class PostgreSqlDesignTimeDbContextFactory : IDesignTimeDbContextFactory<PostgreSqlApplicationDbContext>
{
    public PostgreSqlApplicationDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<PostgreSqlApplicationDbContext>();
        optionsBuilder.UseNpgsql(
            "Host=localhost;Port=5432;Database=CompetitionDb;Username=postgres;Password=postgres",
            b => b.MigrationsAssembly(typeof(PostgreSqlApplicationDbContext).Assembly.FullName));

        return new PostgreSqlApplicationDbContext(optionsBuilder.Options);
    }
}
