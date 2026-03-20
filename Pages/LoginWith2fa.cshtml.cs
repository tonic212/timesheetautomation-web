using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TimesheetAutomation.Web.Models;
using TimesheetAutomation.Web.Services;

namespace TimesheetAutomation.Web.Pages;

[AllowAnonymous]
public sealed class LoginWith2faModel : PageModel
{
    private readonly IUserAuthService _userAuthService;
    private readonly ITwoFactorService _twoFactorService;

    public LoginWith2faModel(
        IUserAuthService userAuthService,
        ITwoFactorService twoFactorService)
    {
        _userAuthService = userAuthService;
        _twoFactorService = twoFactorService;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public IActionResult OnGet()
    {
        if (string.IsNullOrWhiteSpace(TempData.Peek("PendingTwoFactorLogin") as string))
        {
            return RedirectToPage("/Login");
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        string? pendingJson = TempData.Peek("PendingTwoFactorLogin") as string;
        if (string.IsNullOrWhiteSpace(pendingJson))
        {
            return RedirectToPage("/Login");
        }

        PendingTwoFactorLogin? pending = JsonSerializer.Deserialize<PendingTwoFactorLogin>(pendingJson);
        if (pending is null)
        {
            return RedirectToPage("/Login");
        }

        ApplicationUser? user = await _userAuthService.GetByIdAsync(pending.UserId, cancellationToken);
        if (user is null || !user.IsActive || !user.IsTwoFactorEnabled)
        {
            return RedirectToPage("/Login");
        }

        bool valid;
        if (Input.UseRecoveryCode)
        {
            valid = await _userAuthService.RedeemRecoveryCodeAsync(user.Id, Input.Code, cancellationToken);
        }
        else
        {
            valid = !string.IsNullOrWhiteSpace(user.AuthenticatorKey) &&
                    _twoFactorService.ValidateCode(user.AuthenticatorKey, Input.Code);
        }

        if (!valid)
        {
            ModelState.AddModelError(string.Empty, "Invalid verification code.");
            return Page();
        }

        List<Claim> claims =
        [
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.DisplayName),
            new Claim(ClaimTypes.Email, user.Email)
        ];

        if (user.IsAdmin)
        {
            claims.Add(new Claim(ClaimTypes.Role, "Admin"));
        }

        ClaimsIdentity identity = new(
            claims,
            CookieAuthenticationDefaults.AuthenticationScheme);

        ClaimsPrincipal principal = new(identity);

        AuthenticationProperties properties = new()
        {
            IsPersistent = pending.RememberMe,
            AllowRefresh = true
        };

        if (pending.RememberMe)
        {
            properties.ExpiresUtc = DateTimeOffset.UtcNow.AddDays(30);
        }

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            properties);

        await _userAuthService.UpdateLastLoginUtcAsync(user.Id, cancellationToken);

        TempData.Remove("PendingTwoFactorLogin");
        TempData["StatusMessage"] = "Signed in successfully.";
        return RedirectToPage("/Index");
    }

    public sealed class InputModel
    {
        [Required]
        [Display(Name = "Authenticator or recovery code")]
        public string Code { get; set; } = string.Empty;

        [Display(Name = "Use recovery code")]
        public bool UseRecoveryCode { get; set; }
    }
}