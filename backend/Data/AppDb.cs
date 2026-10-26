using Microsoft.EntityFrameworkCore;
using Requests.Models;

namespace Requests.Data;

public class AppDb : DbContext
{
    public AppDb(DbContextOptions<AppDb> options) : base(options) { }

    public DbSet<ServiceRequest> Requests => Set<ServiceRequest>();
    public DbSet<StatusChange> StatusChanges => Set<StatusChange>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        // Map the string properties onto the existing nvarchar columns.
        b.Entity<ServiceRequest>().Property(x => x.StatusRaw)
            .HasColumnName("Status").HasMaxLength(20).IsRequired();
        b.Entity<ServiceRequest>().Property(x => x.PriorityRaw)
            .HasColumnName("Priority").HasMaxLength(20).IsRequired();

        b.Entity<StatusChange>().Property(x => x.PreviousStatusRaw)
            .HasColumnName("PreviousStatus").HasMaxLength(20).IsRequired();
        b.Entity<StatusChange>().Property(x => x.NewStatusRaw)
            .HasColumnName("NewStatus").HasMaxLength(20).IsRequired();

        b.Entity<StatusChange>()
            .HasOne(x => x.Request)
            .WithMany(x => x.StatusChanges)
            .HasForeignKey(x => x.RequestId)
            .OnDelete(DeleteBehavior.Cascade);

        // Text filters (OrganisationName, AssignedTo) use LIKE '%..%', which a
        // B-tree index cannot seek - deliberately not indexed.

        b.Entity<ServiceRequest>()
            .HasIndex(x => new { x.CreatedAt, x.Id })
            .HasDatabaseName("IX_Requests_CreatedAt_Id")
            .IsDescending(true, true);

        b.Entity<ServiceRequest>()
            .HasIndex(x => new { x.StatusRaw, x.CreatedAt, x.Id })
            .HasDatabaseName("IX_Requests_Status_CreatedAt_Id")
            .IsDescending(false, true, true);

        b.Entity<ServiceRequest>()
            .HasIndex(x => new { x.PriorityRaw, x.CreatedAt, x.Id })
            .HasDatabaseName("IX_Requests_Priority_CreatedAt_Id")
            .IsDescending(false, true, true);

        b.Entity<ServiceRequest>()
            .HasIndex(x => new { x.StatusRaw, x.PriorityRaw, x.CreatedAt })
            .HasDatabaseName("IX_Requests_Status_Priority_CreatedAt");

        b.Entity<ServiceRequest>()
            .HasIndex(x => new { x.UpdatedAt, x.Id })
            .HasDatabaseName("IX_Requests_UpdatedAt_Id")
            .IsDescending(true, true);

        b.Entity<StatusChange>()
            .HasIndex(x => new { x.RequestId, x.ChangedAt })
            .HasDatabaseName("IX_StatusChanges_RequestId_ChangedAt")
            .IsDescending(false, true);
    }
}