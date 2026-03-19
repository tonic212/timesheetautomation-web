using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace TimesheetAutomation.Web.Pages;

[Authorize]
public sealed class IndexModel : PageModel
{
    public string DisplayName { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;

    public void OnGet()
    {
        DisplayName = User.FindFirstValue(ClaimTypes.Name) ?? string.Empty;
        Email = User.FindFirstValue(ClaimTypes.Email) ?? string.Empty;
    }
}