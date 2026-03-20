using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace TimesheetAutomation.Web.Pages;

[Authorize]
public sealed class IndexModel : PageModel
{
    public string DisplayName { get; private set; } = string.Empty;

    public string? StatusMessage { get; private set; }

    public void OnGet()
    {
        DisplayName = User.Identity?.Name ?? "User";
        StatusMessage = TempData["StatusMessage"] as string;
    }
}