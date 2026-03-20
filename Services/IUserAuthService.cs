using TimesheetAutomation.Web.Models;

namespace TimesheetAutomation.Web.Services;

public interface IUserAuthService
{
    Task<(bool Succeeded, string? ErrorMessage, ApplicationUser? User)> RegisterAsync(
        string displayName,
        string email,
        string password,
        CancellationToken cancellationToken);

    Task<ApplicationUser?> ValidateCredentialsAsync(
        string email,
        string password,
        CancellationToken cancellationToken);

    Task<ApplicationUser?> GetByEmailAsync(
        string email,
        CancellationToken cancellationToken);

    Task<ApplicationUser?> GetByIdAsync(
        Guid userId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ApplicationUser>> GetAllUsersAsync(CancellationToken cancellationToken);

    Task SetTwoFactorAsync(
        Guid userId,
        string authenticatorKey,
        string[] recoveryCodes,
        CancellationToken cancellationToken);

    Task DisableTwoFactorAsync(
        Guid userId,
        CancellationToken cancellationToken);

    Task<bool> RedeemRecoveryCodeAsync(
        Guid userId,
        string recoveryCode,
        CancellationToken cancellationToken);

    Task UpdateLastLoginUtcAsync(
        Guid userId,
        CancellationToken cancellationToken);

    Task SetPasswordAsync(
        Guid userId,
        string newPassword,
        CancellationToken cancellationToken);

    Task SetAdminAsync(
        Guid userId,
        bool isAdmin,
        CancellationToken cancellationToken);

    Task<string?> CreatePasswordResetTokenAsync(
        string email,
        CancellationToken cancellationToken);

    Task<bool> ResetPasswordAsync(
        string token,
        string newPassword,
        CancellationToken cancellationToken);
}