using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TimesheetAutomation.Web.Data;
using TimesheetAutomation.Web.Models;

namespace TimesheetAutomation.Web.Services;

public sealed class UserAuthService : IUserAuthService
{
    private readonly AuthDbContext _dbContext;
    private readonly PasswordHasher<ApplicationUser> _passwordHasher;

    public UserAuthService(AuthDbContext dbContext)
    {
        _dbContext = dbContext;
        _passwordHasher = new PasswordHasher<ApplicationUser>();
    }

    public async Task<(bool Succeeded, string? ErrorMessage, ApplicationUser? User)> RegisterAsync(
        string displayName,
        string email,
        string password,
        CancellationToken cancellationToken)
    {
        string trimmedName = string.IsNullOrWhiteSpace(displayName) ? string.Empty : displayName.Trim();
        string trimmedEmail = string.IsNullOrWhiteSpace(email) ? string.Empty : email.Trim();

        if (string.IsNullOrWhiteSpace(trimmedName))
        {
            return (false, "Display name is required.", null);
        }

        if (string.IsNullOrWhiteSpace(trimmedEmail))
        {
            return (false, "Email is required.", null);
        }

        if (!trimmedEmail.EndsWith("@chemwatch.net", StringComparison.OrdinalIgnoreCase))
        {
            return (false, "Only @chemwatch.net email addresses can register.", null);
        }

        string normalizedEmail = trimmedEmail.ToUpperInvariant();

        bool exists = await _dbContext.Users
            .AnyAsync(x => x.NormalizedEmail == normalizedEmail, cancellationToken);

        if (exists)
        {
            return (false, "An account with this email already exists.", null);
        }

        ApplicationUser user = new()
        {
            Email = trimmedEmail,
            NormalizedEmail = normalizedEmail,
            DisplayName = trimmedName,
            HostedDomain = "chemwatch.net",
            GoogleSubject = $"local:{Guid.NewGuid():N}",
            IsActive = true,
            IsAdmin = false,
            CreatedUtc = DateTime.UtcNow
        };

        user.PasswordHash = _passwordHasher.HashPassword(user, password);

        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return (true, null, user);
    }

    public async Task<ApplicationUser?> ValidateCredentialsAsync(
        string email,
        string password,
        CancellationToken cancellationToken)
    {
        string trimmedEmail = string.IsNullOrWhiteSpace(email) ? string.Empty : email.Trim();
        if (string.IsNullOrWhiteSpace(trimmedEmail))
        {
            return null;
        }

        string normalizedEmail = trimmedEmail.ToUpperInvariant();

        ApplicationUser? user = await _dbContext.Users
            .SingleOrDefaultAsync(x => x.NormalizedEmail == normalizedEmail, cancellationToken);

        if (user is null || !user.IsActive)
        {
            return null;
        }

        PasswordVerificationResult result =
            _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);

        if (result == PasswordVerificationResult.Failed)
        {
            return null;
        }

        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = _passwordHasher.HashPassword(user, password);
        }

        user.LastLoginUtc = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return user;
    }

    public async Task<ApplicationUser?> GetByEmailAsync(
        string email,
        CancellationToken cancellationToken)
    {
        string normalizedEmail = email.Trim().ToUpperInvariant();

        return await _dbContext.Users
            .SingleOrDefaultAsync(x => x.NormalizedEmail == normalizedEmail, cancellationToken);
    }
}