using Microsoft.EntityFrameworkCore;
using TimesheetAutomation.Web.Data;
using TimesheetAutomation.Web.Models;

namespace TimesheetAutomation.Web.Security;

public interface IGoogleUserProvisioningService
{
    Task<ApplicationUser> GetOrCreateUserAsync(
        string email,
        string displayName,
        string hostedDomain,
        string googleSubject,
        CancellationToken cancellationToken);
}

public sealed class GoogleUserProvisioningService : IGoogleUserProvisioningService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<GoogleUserProvisioningService> _logger;

    public GoogleUserProvisioningService(
        ApplicationDbContext dbContext,
        ILogger<GoogleUserProvisioningService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<ApplicationUser> GetOrCreateUserAsync(
        string email,
        string displayName,
        string hostedDomain,
        string googleSubject,
        CancellationToken cancellationToken)
    {
        string normalizedEmail = email.Trim().ToUpperInvariant();

        ApplicationUser? existingUser = await _dbContext.Users
            .SingleOrDefaultAsync(x => x.NormalizedEmail == normalizedEmail, cancellationToken);

        if (existingUser is not null)
        {
            existingUser.DisplayName = displayName.Trim();
            existingUser.HostedDomain = hostedDomain.Trim();
            existingUser.GoogleSubject = googleSubject.Trim();
            existingUser.LastLoginUtc = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync(cancellationToken);
            return existingUser;
        }

        ApplicationUser user = new()
        {
            Email = email.Trim(),
            NormalizedEmail = normalizedEmail,
            DisplayName = displayName.Trim(),
            HostedDomain = hostedDomain.Trim(),
            GoogleSubject = googleSubject.Trim(),
            IsActive = true,
            IsAdmin = false,
            CreatedUtc = DateTime.UtcNow,
            LastLoginUtc = DateTime.UtcNow
        };

        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Created new application user for {Email}.", user.Email);

        return user;
    }
}