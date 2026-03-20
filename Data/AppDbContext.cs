using Microsoft.EntityFrameworkCore;
using TimesheetAutomation.Web.Models;

namespace TimesheetAutomation.Web.Data;

public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<DailyTimeEntry> DailyTimeEntries => Set<DailyTimeEntry>();

    public DbSet<TimeEntryAudit> TimeEntryAudits => Set<TimeEntryAudit>();

    public DbSet<FortnightExport> FortnightExports => Set<FortnightExport>();

    public DbSet<TilLedgerEntry> TilLedgerEntries => Set<TilLedgerEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<DailyTimeEntry>()
            .HasIndex(x => new { x.UserId, x.WorkDate })
            .IsUnique();

        modelBuilder.Entity<TimeEntryAudit>()
            .HasIndex(x => new { x.DailyTimeEntryId, x.ChangedUtc });

        modelBuilder.Entity<FortnightExport>()
            .HasIndex(x => x.UserId);

        modelBuilder.Entity<TilLedgerEntry>()
            .HasIndex(x => x.UserId);

        modelBuilder.Entity<TilLedgerEntry>()
            .HasIndex(x => new { x.UserId, x.SortOrder })
            .IsUnique();

        modelBuilder.Entity<TilLedgerEntry>()
            .HasIndex(x => new { x.SourceDailyTimeEntryId, x.SourceKind });

        modelBuilder.Entity<TilLedgerEntry>()
            .HasOne(x => x.SourceDailyTimeEntry)
            .WithMany(x => x.TilLedgerEntries)
            .HasForeignKey(x => x.SourceDailyTimeEntryId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}