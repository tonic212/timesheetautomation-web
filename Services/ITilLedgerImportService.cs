namespace TimesheetAutomation.Web.Services;

public interface ITilLedgerImportService
{
    Task<int> ImportLatestWorkbookAsync(Guid userId, Stream fileStream, CancellationToken cancellationToken);
}