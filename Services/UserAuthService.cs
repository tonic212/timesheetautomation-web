using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TimesheetAutomation.Web.Data;
using TimesheetAutomation.Web.Models;

namespace TimesheetAutomation.Web.Services;

public sealed class UserAuthService : IUserAuthService
{
    private readonly AuthDbContext _dbContext;
    private readonly PasswordHasher<ApplicationUser> _passwordHasher;
    private readonly ITwoFactorService _twoFactorService;

    public UserAuthService(AuthDbContext dbContext, ITwoFactorService twoFactorService)
    {
        _dbContext = dbContext;
        _passwordHasher = new PasswordHasher<ApplicationUser>();
        _twoFactorService = twoFactorService;
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

        bool isFirstUser = !await _dbContext.Users.AnyAsync(cancellationToken);

        ApplicationUser user = new()
        {
            Email = trimmedEmail,
            NormalizedEmail = normalizedEmail,
            DisplayName = trimmedName,
            HostedDomain = "chemwatch.net",
            GoogleSubject = $"local:{Guid.NewGuid():N}",
            IsActive = true,
            IsAdmin = isFirstUser,
            IsTwoFactorEnabled = false,
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
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return user;
    }

    public async Task<ApplicationUser?> GetByEmailAsync(string email, CancellationToken cancellationToken)
    {
        string normalizedEmail = email.Trim().ToUpperInvariant();

        return await _dbContext.Users
            .SingleOrDefaultAsync(x => x.NormalizedEmail == normalizedEmail, cancellationToken);
    }

    public async Task<ApplicationUser?> GetByIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await _dbContext.Users
            .SingleOrDefaultAsync(x => x.Id == userId, cancellationToken);
    }

    public async Task<IReadOnlyList<ApplicationUser>> GetAllUsersAsync(CancellationToken cancellationToken)
    {
        return await _dbContext.Users
            .OrderBy(x => x.DisplayName)
            .ThenBy(x => x.Email)
            .ToListAsync(cancellationToken);
    }

    public async Task SetTwoFactorAsync(
        Guid userId,
        string authenticatorKey,
        string[] recoveryCodes,
        CancellationToken cancellationToken)
    {
        ApplicationUser user = await _dbContext.Users
            .SingleAsync(x => x.Id == userId, cancellationToken);

        user.AuthenticatorKey = authenticatorKey;
        user.IsTwoFactorEnabled = true;
        user.RecoveryCodes = string.Join(';', recoveryCodes);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DisableTwoFactorAsync(Guid userId, CancellationToken cancellationToken)
    {
        ApplicationUser user = await _dbContext.Users
            .SingleAsync(x => x.Id == userId, cancellationToken);

        user.AuthenticatorKey = null;
        user.IsTwoFactorEnabled = false;
        user.RecoveryCodes = null;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> RedeemRecoveryCodeAsync(
        Guid userId,
        string recoveryCode,
        CancellationToken cancellationToken)
    {
        ApplicationUser user = await _dbContext.Users
            .SingleAsync(x => x.Id == userId, cancellationToken);

        string[] existingCodes = string.IsNullOrWhiteSpace(user.RecoveryCodes)
            ? Array.Empty<string>()
            : user.RecoveryCodes.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        bool valid = _twoFactorService.ValidateRecoveryCode(existingCodes, recoveryCode, out string[] remainingCodes);
        if (!valid)
        {
            return false;
        }

        user.RecoveryCodes = remainingCodes.Length == 0
            ? null
            : string.Join(';', remainingCodes);

        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task UpdateLastLoginUtcAsync(Guid userId, CancellationToken cancellationToken)
    {
        ApplicationUser user = await _dbContext.Users
            .SingleAsync(x => x.Id == userId, cancellationToken);

        user.LastLoginUtc = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task SetPasswordAsync(Guid userId, string newPassword, CancellationToken cancellationToken)
    {
        ApplicationUser user = await _dbContext.Users
            .SingleAsync(x => x.Id == userId, cancellationToken);

        user.PasswordHash = _passwordHasher.HashPassword(user, newPassword);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task SetAdminAsync(Guid userId, bool isAdmin, CancellationToken cancellationToken)
    {
        ApplicationUser user = await _dbContext.Users
            .SingleAsync(x => x.Id == userId, cancellationToken);

        user.IsAdmin = isAdmin;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<string?> CreatePasswordResetTokenAsync(string email, CancellationToken cancellationToken)
    {
        string normalizedEmail = email.Trim().ToUpperInvariant();

        ApplicationUser? user = await _dbContext.Users
            .SingleOrDefaultAsync(x => x.NormalizedEmail == normalizedEmail && x.IsActive, cancellationToken);

        if (user is null)
        {
            return null;
        }

        byte[] tokenBytes = RandomNumberGenerator.GetBytes(32);
        string token = Convert.ToBase64String(tokenBytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');

        string tokenHash = ComputeSha256(token);

        PasswordResetToken resetToken = new()
        {
            UserId = user.Id,
            TokenHash = tokenHash,
            CreatedUtc = DateTime.UtcNow,
            ExpiresUtc = DateTime.UtcNow.AddMinutes(30)
        };

        _dbContext.PasswordResetTokens.Add(resetToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return token;
    }

    public async Task<bool> ResetPasswordAsync(string token, string newPassword, CancellationToken cancellationToken)
    {
        string tokenHash = ComputeSha256(token);

        PasswordResetToken? resetToken = await _dbContext.PasswordResetTokens
            .SingleOrDefaultAsync(
                x => x.TokenHash == tokenHash &&
                     x.UsedUtc == null &&
                     x.ExpiresUtc > DateTime.UtcNow,
                cancellationToken);

        if (resetToken is null)
        {
            return false;
        }

        ApplicationUser user = await _dbContext.Users
            .SingleAsync(x => x.Id == resetToken.UserId, cancellationToken);

        user.PasswordHash = _passwordHasher.HashPassword(user, newPassword);
        resetToken.UsedUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static string ComputeSha256(string value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(value);
        byte[] hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }
}