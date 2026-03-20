using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Security.Claims;
using TimesheetAutomation.Web.Models;
using TimesheetAutomation.Web.Services;

namespace TimesheetAutomation.Web.Pages;

[Authorize(Roles = "Admin")]
public sealed class AdminSecurityModel : PageModel
{
    private readonly IUserAuthService _userAuthService;

    public AdminSecurityModel(IUserAuthService userAuthService)
    {
        _userAuthService = userAuthService;
    }

    public IReadOnlyList<ApplicationUser> Users { get; private set; } = Array.Empty<ApplicationUser>();

    [TempData]
    public string? StatusMessage { get; set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Users = await _userAuthService.GetAllUsersAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostResetPasswordAsync(Guid targetUserId, string newPassword, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 8)
        {
            StatusMessage = "Password reset failed. New password must be at least 8 characters.";
            return RedirectToPage("/AdminSecurity");
        }

        await _userAuthService.SetPasswordAsync(targetUserId, newPassword, cancellationToken);
        StatusMessage = "Password updated successfully.";
        return RedirectToPage("/AdminSecurity");
    }

    public async Task<IActionResult> OnPostResetMfaAsync(Guid targetUserId, CancellationToken cancellationToken)
    {
        await _userAuthService.DisableTwoFactorAsync(targetUserId, cancellationToken);
        StatusMessage = "MFA reset successfully.";
        return RedirectToPage("/AdminSecurity");
    }

    public async Task<IActionResult> OnPostGrantAdminAsync(Guid targetUserId, CancellationToken cancellationToken)
    {
        await _userAuthService.SetAdminAsync(targetUserId, true, cancellationToken);
        StatusMessage = "Admin access granted successfully.";
        return RedirectToPage("/AdminSecurity");
    }

    public async Task<IActionResult> OnPostRemoveAdminAsync(Guid targetUserId, CancellationToken cancellationToken)
    {
        string? currentUserIdText = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (Guid.TryParse(currentUserIdText, out Guid currentUserId) && currentUserId == targetUserId)
        {
            StatusMessage = "You cannot remove admin access from your own account.";
            return RedirectToPage("/AdminSecurity");
        }

        await _userAuthService.SetAdminAsync(targetUserId, false, cancellationToken);
        StatusMessage = "Admin access removed successfully.";
        return RedirectToPage("/AdminSecurity");
    }
}