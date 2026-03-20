using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TimesheetAutomation.Web.Security;
using TimesheetAutomation.Web.Services;

namespace TimesheetAutomation.Web.Pages;

[Authorize]
public sealed class TilLedgerImportModel : PageModel
{
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly ITilLedgerImportService _tilLedgerImportService;

    public TilLedgerImportModel(
        ICurrentUserAccessor currentUserAccessor,
        ITilLedgerImportService tilLedgerImportService)
    {
        _currentUserAccessor = currentUserAccessor;
        _tilLedgerImportService = tilLedgerImportService;
    }

    [BindProperty]
    public IFormFile? UploadFile { get; set; }

    [TempData]
    public string? StatusMessage { get; set; }

    public void OnGet()
    {
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

        StatusMessage = $"{importedCount} historical TIL ledger row(s) imported successfully.";
        return RedirectToPage("/TilLedgerImport");
    }
}