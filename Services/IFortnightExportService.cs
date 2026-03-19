using TimesheetAutomation.Web.Models;

namespace TimesheetAutomation.Web.Services;

public interface IFortnightExportService
{
    Task<FortnightExport> GenerateCurrentFortnightAsync(Guid userId, CancellationToken cancellationToken);

    Task<IReadOnlyList<FortnightExport>> GetExportsAsync(Guid userId, CancellationToken cancellationToken);

    Task<FortnightExport?> GetExportByIdAsync(Guid userId, Guid exportId, CancellationToken cancellationToken);
}