using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TimesheetAutomation.Web.Services;

namespace TimesheetAutomation.Web.Pages;

[AllowAnonymous]
public sealed class ResetPasswordModel : PageModel
{
    private readonly IUserAuthService _userAuthService;

    public ResetPasswordModel(IUserAuthService userAuthService)
    {
        _userAuthService = userAuthService;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public IActionResult OnGet(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return RedirectToPage("/Login");
        }

        Input.Token = token;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        bool success = await _userAuthService.ResetPasswordAsync(
            Input.Token,
            Input.NewPassword,
            cancellationToken);

        if (!success)
        {
            ModelState.AddModelError(string.Empty, "This reset link is invalid or has expired.");
            return Page();
        }

        TempData["StatusMessage"] = "Password reset successfully. Please sign in.";
        return RedirectToPage("/Login");
    }

    public sealed class InputModel
    {
        [Required]
        public string Token { get; set; } = string.Empty;

        [Required]
        [MinLength(8)]
        [DataType(DataType.Password)]
        [Display(Name = "New password")]
        public string NewPassword { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        [Compare(nameof(NewPassword))]
        [Display(Name = "Confirm new password")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}