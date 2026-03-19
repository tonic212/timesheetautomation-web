using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TimesheetAutomation.Web.Models;
using TimesheetAutomation.Web.Security;
using TimesheetAutomation.Web.Services;

namespace TimesheetAutomation.Web.Pages;

[Authorize]
public sealed class ExportsModel : PageModel
{
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IFortnightExportService _fortnightExportService;

    public ExportsModel(
        ICurrentUserAccessor currentUserAccessor,
        IFortnightExportService fortnightExportService)
    {
        _currentUserAccessor = currentUserAccessor;
        _fortnightExportService = fortnightExportService;
    }

    public IReadOnlyList<FortnightExport> Exports { get; private set; } = Array.Empty<FortnightExport>();

    [TempData]
    public string? StatusMessage { get; set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Guid userId = _currentUserAccessor.GetRequiredUserId(User);
        Exports = await _fortnightExportService.GetExportsAsync(userId, cancellationToken);
    }

    public async Task<IActionResult> OnPostGenerateAsync(CancellationToken cancellationToken)
    {
        Guid userId = _currentUserAccessor.GetRequiredUserId(User);

        FortnightExport export = await _fortnightExportService.GenerateCurrentFortnightAsync(userId, cancellationToken);
        StatusMessage = $"Export generated: {export.FileName}";

        return RedirectToPage("/Exports");
    }

    public async Task<IActionResult> OnGetDownloadAsync(Guid exportId, CancellationToken cancellationToken)
    {
        Guid userId = _currentUserAccessor.GetRequiredUserId(User);

        FortnightExport? export = await _fortnightExportService.GetExportByIdAsync(userId, exportId, cancellationToken);
        if (export is null)
        {
            return NotFound();
        }

        if (!System.IO.File.Exists(export.FilePath))
        {
            return NotFound();
        }

        byte[] fileBytes = await System.IO.File.ReadAllBytesAsync(export.FilePath, cancellationToken);

        return File(
            fileBytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            export.FileName);
    }
}