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
}