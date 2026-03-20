using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TimesheetAutomation.Web.Models;
using TimesheetAutomation.Web.Services;

namespace TimesheetAutomation.Web.Pages;

[AllowAnonymous]
public sealed class RegisterModel : PageModel
{
    private readonly IUserAuthService _userAuthService;

    public RegisterModel(IUserAuthService userAuthService)
    {
        _userAuthService = userAuthService;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

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

        (bool succeeded, string? errorMessage, ApplicationUser? user) =
            await _userAuthService.RegisterAsync(
                Input.DisplayName,
                Input.Email,
                Input.Password,
                cancellationToken);

        if (!succeeded || user is null)
        {
            ModelState.AddModelError(string.Empty, errorMessage ?? "Registration failed.");
            return Page();
        }

        List<Claim> claims =
        [
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.DisplayName),
            new Claim(ClaimTypes.Email, user.Email)
        ];

        ClaimsIdentity identity = new(
            claims,
            CookieAuthenticationDefaults.AuthenticationScheme);

        ClaimsPrincipal principal = new(identity);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddDays(30),
                AllowRefresh = true
            });

        TempData["StatusMessage"] = "Account created successfully. Please complete the welcome setup step.";
        return RedirectToPage("/Welcome");
    }

    public sealed class InputModel
    {
        [Required]
        [Display(Name = "Display name")]
        public string DisplayName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;

        [Required]
        [MinLength(8)]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        [Compare(nameof(Password))]
        [Display(Name = "Confirm password")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}