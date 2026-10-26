using Microsoft.EntityFrameworkCore;
using Requests.Models;

namespace Requests.Data;

public class AppDb : DbContext
{
    public AppDb(DbContextOptions<AppDb> options) : base(options) { }

    public DbSet<ServiceRequest> Requests => Set<ServiceRequest>();
    public DbSet<StatusChange> StatusChanges => Set<StatusChange>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ServiceRequest>()
            .HasIndex(r => new { r.OrganizationName, r.Status })
            .HasDatabaseName("IX_Requests_Organization_Status");
    }
}