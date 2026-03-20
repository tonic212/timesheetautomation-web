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
public sealed class LoginModel : PageModel
{
    private readonly IUserAuthService _userAuthService;

    public LoginModel(IUserAuthService userAuthService)
    {
        _userAuthService = userAuthService;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    [TempData]
    public string? StatusMessage { get; set; }

    public IActionResult OnGet()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToPage("/Index");
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        ApplicationUser? user = await _userAuthService.ValidateCredentialsAsync(
            Input.Email,
            Input.Password,
            cancellationToken);

        if (user is null)
        {
            ModelState.AddModelError(string.Empty, "Invalid email or password.");
            return Page();
        }

        if (user.IsTwoFactorEnabled)
        {
            PendingTwoFactorLogin pending = new()
            {
                UserId = user.Id,
                RememberMe = Input.RememberMe
            };

            TempData["PendingTwoFactorLogin"] = JsonSerializer.Serialize(pending);
            return RedirectToPage("/LoginWith2fa");
        }

        await SignInUserAsync(user, Input.RememberMe, cancellationToken);

        TempData["StatusMessage"] = "Signed in successfully.";
        return RedirectToPage("/Index");
    }

    private async Task SignInUserAsync(ApplicationUser user, bool rememberMe, CancellationToken cancellationToken)
    {
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
            IsPersistent = rememberMe,
            AllowRefresh = true
        };

        if (rememberMe)
        {
            properties.ExpiresUtc = DateTimeOffset.UtcNow.AddDays(30);
        }

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            properties);

        await _userAuthService.UpdateLastLoginUtcAsync(user.Id, cancellationToken);
    }

    public sealed class InputModel
    {
        [Required]
        [EmailAddress]
        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; } = string.Empty;

        [Display(Name = "Remember me for 30 days")]
        public bool RememberMe { get; set; }
    }
}