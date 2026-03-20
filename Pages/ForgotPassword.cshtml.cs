using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TimesheetAutomation.Web.Services;

namespace TimesheetAutomation.Web.Pages;

[AllowAnonymous]
public sealed class ForgotPasswordModel : PageModel
{
    private readonly IUserAuthService _userAuthService;
    private readonly IEmailSender _emailSender;

    public ForgotPasswordModel(
        IUserAuthService userAuthService,
        IEmailSender emailSender)
    {
        _userAuthService = userAuthService;
        _emailSender = emailSender;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    [TempData]
    public string? StatusMessage { get; set; }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        string? token = await _userAuthService.CreatePasswordResetTokenAsync(Input.Email, cancellationToken);

        if (!string.IsNullOrWhiteSpace(token))
        {
            string resetUrl = Url.Page(
                "/ResetPassword",
                null,
                new { token },
                Request.Scheme)!;

            string body = $"""
                <p>Hello,</p>
                <p>A password reset was requested for your Timesheet Automation account.</p>
                <p><a href="{resetUrl}">Click here to reset your password</a></p>
                <p>This link expires in 30 minutes.</p>
                <p>If you did not request this, you can ignore this email.</p>
                """;

            await _emailSender.SendAsync(
                Input.Email.Trim(),
                "Timesheet Automation - Password Reset",
                body,
                cancellationToken);
        }

        StatusMessage = "If that account exists, a password reset link has been sent.";
        return RedirectToPage("/ForgotPassword");
    }

    public sealed class InputModel
    {
        [Required]
        [EmailAddress]
        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;
    }
}