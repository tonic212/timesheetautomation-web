using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace TimesheetAutomation.Web.Pages;

public sealed class LoginModel : PageModel
{
    public IActionResult OnGet()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToPage("/Index");
        }

        return Page();
    }

    public IActionResult OnPost()
    {
        AuthenticationProperties properties = new()
        {
            RedirectUri = Url.Page("/Index")
        };

        return Challenge(properties, GoogleDefaults.AuthenticationScheme);
    }
}