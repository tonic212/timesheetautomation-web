using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TimesheetAutomation.Web.Security;
using TimesheetAutomation.Web.Services;

namespace TimesheetAutomation.Web.Pages;

[Authorize]
public sealed class WelcomeModel : PageModel
{
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly ITilLedgerImportService _tilLedgerImportService;
    private readonly IUserAuthService _userAuthService;

    public WelcomeModel(
        ICurrentUserAccessor currentUserAccessor,
        ITilLedgerImportService tilLedgerImportService,
        IUserAuthService userAuthService)
    {
        _currentUserAccessor = currentUserAccessor;
        _tilLedgerImportService = tilLedgerImportService;
        _userAuthService = userAuthService;
    }

    [BindProperty]
    public IFormFile? UploadFile { get; set; }

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

        if (!user.IsTwoFactorEnabled)
        {
            return RedirectToPage("/EnableAuthenticator");
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (UploadFile is null || UploadFile.Length == 0)
        {
            ModelState.AddModelError(nameof(UploadFile), "Please select a workbook to upload.");
            return Page();
        }

        string extension = Path.GetExtension(UploadFile.FileName);
        if (!string.Equals(extension, ".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(UploadFile), "Only .xlsx files are supported.");
            return Page();
        }

        Guid userId = _currentUserAccessor.GetRequiredUserId(User);

        await using Stream stream = UploadFile.OpenReadStream();
        int importedCount = await _tilLedgerImportService.ImportLatestWorkbookAsync(userId, stream, cancellationToken);

        TempData["StatusMessage"] = $"{importedCount} historical TIL ledger row(s) imported successfully.";
        return RedirectToPage("/Index");
    }

    public IActionResult OnPostSkip()
    {
        TempData["StatusMessage"] = "Latest timesheet upload skipped. You can import it later from the app.";
        return RedirectToPage("/Index");
    }
}