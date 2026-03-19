using Microsoft.EntityFrameworkCore;
using TimesheetAutomation.Web.Models;

namespace TimesheetAutomation.Web.Data;

public sealed class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<ApplicationUser> Users => Set<ApplicationUser>();
    public DbSet<DailyTimeEntry> DailyTimeEntries => Set<DailyTimeEntry>();
    public DbSet<FortnightExport> FortnightExports => Set<FortnightExport>();
    public DbSet<TimeEntryAudit> TimeEntryAudits => Set<TimeEntryAudit>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ApplicationUser>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.HasIndex(x => x.Email).IsUnique();
            entity.HasIndex(x => x.NormalizedEmail).IsUnique();
            entity.HasIndex(x => x.GoogleSubject).IsUnique();

            entity.Property(x => x.Email).IsRequired().HasMaxLength(256);
            entity.Property(x => x.NormalizedEmail).IsRequired().HasMaxLength(256);
            entity.Property(x => x.DisplayName).IsRequired().HasMaxLength(256);
            entity.Property(x => x.HostedDomain).IsRequired().HasMaxLength(100);
            entity.Property(x => x.GoogleSubject).IsRequired().HasMaxLength(100);
        });

        modelBuilder.Entity<DailyTimeEntry>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.HasIndex(x => new { x.UserId, x.WorkDate }).IsUnique();

            entity.Property(x => x.Notes).HasMaxLength(500);

            entity.HasOne(x => x.User)
                .WithMany(x => x.DailyTimeEntries)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<FortnightExport>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.Property(x => x.FilePath).IsRequired().HasMaxLength(500);
            entity.Property(x => x.FileName).IsRequired().HasMaxLength(255);

            entity.HasOne(x => x.User)
                .WithMany(x => x.FortnightExports)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TimeEntryAudit>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.HasIndex(x => new { x.DailyTimeEntryId, x.ChangedUtc });

            entity.Property(x => x.FieldName).IsRequired().HasMaxLength(100);
            entity.Property(x => x.OldValue).HasMaxLength(200);
            entity.Property(x => x.NewValue).HasMaxLength(200);
        });
    }
}