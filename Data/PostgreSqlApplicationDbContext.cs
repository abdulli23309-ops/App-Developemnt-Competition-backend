using CompetitionApi.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace CompetitionApi.Data;

/// <summary>
/// PostgreSQL-specific DbContext for production deployment on Railway.
/// Inherits all entities and configurations from ApplicationDbContext,
/// applying PostgreSQL-specific mappings (such as bytea concurrency tokens)
/// and maintaining an independent migration set in Migrations/PostgreSql.
/// </summary>
public class PostgreSqlApplicationDbContext : ApplicationDbContext
{
    public PostgreSqlApplicationDbContext(DbContextOptions<PostgreSqlApplicationDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // PostgreSQL-compatible RowVersion concurrency token mapping
        // In PostgreSQL, bytea with IsConcurrencyToken provides optimistic concurrency without SQL Server rowversion.
        modelBuilder.Entity<Complaint>(entity =>
        {
            entity.Property(c => c.RowVersion)
                .HasColumnType("bytea")
                .IsConcurrencyToken()
                .HasDefaultValueSql("substring(decode(md5(random()::text), 'hex') from 1 for 8)");
        });
    }
}
