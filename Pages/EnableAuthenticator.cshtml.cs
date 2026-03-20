using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TimesheetAutomation.Web.Security;
using TimesheetAutomation.Web.Services;

namespace TimesheetAutomation.Web.Pages;

[Authorize]
public sealed class EnableAuthenticatorModel : PageModel
{
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IUserAuthService _userAuthService;
    private readonly ITwoFactorService _twoFactorService;

    public EnableAuthenticatorModel(
        ICurrentUserAccessor currentUserAccessor,
        IUserAuthService userAuthService,
        ITwoFactorService twoFactorService)
    {
        _currentUserAccessor = currentUserAccessor;
        _userAuthService = userAuthService;
        _twoFactorService = twoFactorService;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string ManualEntryKey { get; private set; } = string.Empty;

    public string AuthenticatorUri { get; private set; } = string.Empty;

    public string QrCodeDataUri { get; private set; } = string.Empty;

    public bool IsAlreadyEnabled { get; private set; }

    public List<string> RecoveryCodes { get; private set; } = new();

    [TempData]
    public string? PendingAuthenticatorKey { get; set; }

    [TempData]
    public string? StatusMessage { get; set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        Guid userId = _currentUserAccessor.GetRequiredUserId(User);
        var user = await _userAuthService.GetByIdAsync(userId, cancellationToken);

        if (user is null)
        {
            return RedirectToPage("/Login");
        }

        IsAlreadyEnabled = user.IsTwoFactorEnabled;

        if (!IsAlreadyEnabled)
        {
            string key = _twoFactorService.GenerateNewSecretKey();
            PendingAuthenticatorKey = key;
            ManualEntryKey = _twoFactorService.BuildManualEntryKey(key);
            AuthenticatorUri = _twoFactorService.BuildAuthenticatorUri(user.Email, key);
            QrCodeDataUri = _twoFactorService.BuildQrCodeDataUri(AuthenticatorUri);
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        Guid userId = _currentUserAccessor.GetRequiredUserId(User);
        var user = await _userAuthService.GetByIdAsync(userId, cancellationToken);

        if (user is null)
        {
            return RedirectToPage("/Login");
        }

        string? key = PendingAuthenticatorKey;
        if (string.IsNullOrWhiteSpace(key))
        {
            ModelState.AddModelError(string.Empty, "Authenticator setup expired. Please reload the page.");
            return await OnGetAsync(cancellationToken);
        }

        if (!_twoFactorService.ValidateCode(key, Input.Code))
        {
            ManualEntryKey = _twoFactorService.BuildManualEntryKey(key);
            AuthenticatorUri = _twoFactorService.BuildAuthenticatorUri(user.Email, key);
            QrCodeDataUri = _twoFactorService.BuildQrCodeDataUri(AuthenticatorUri);
            ModelState.AddModelError(string.Empty, "Invalid authenticator code.");
            return Page();
        }

        string[] recoveryCodes = _twoFactorService.GenerateRecoveryCodes(8);
        await _userAuthService.SetTwoFactorAsync(userId, key, recoveryCodes, cancellationToken);

        RecoveryCodes = recoveryCodes.ToList();
        PendingAuthenticatorKey = null;

        TempData["StatusMessage"] = "Authenticator enabled successfully.";
        return RedirectToPage("/Welcome");
    }

    public sealed class InputModel
    {
        [Required]
        [Display(Name = "Verification code")]
        public string Code { get; set; } = string.Empty;
    }
}