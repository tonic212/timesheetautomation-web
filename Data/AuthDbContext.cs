using Microsoft.EntityFrameworkCore;
using TimesheetAutomation.Web.Models;

namespace TimesheetAutomation.Web.Data;

public sealed class AuthDbContext : DbContext
{
    public AuthDbContext(DbContextOptions<AuthDbContext> options)
        : base(options)
    {
    }

    public DbSet<ApplicationUser> Users => Set<ApplicationUser>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ApplicationUser>()
            .HasIndex(x => x.Email)
            .IsUnique();

        modelBuilder.Entity<ApplicationUser>()
            .HasIndex(x => x.NormalizedEmail)
            .IsUnique();

        modelBuilder.Entity<ApplicationUser>()
            .HasIndex(x => x.GoogleSubject)
            .IsUnique();
    }
}