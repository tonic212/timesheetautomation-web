using TimesheetAutomation.Web.Models;

namespace TimesheetAutomation.Web.Services;

public interface ITimeEntryService
{
    Task<DailyTimeEntry?> GetByUserAndDateAsync(Guid userId, DateOnly workDate, CancellationToken cancellationToken);

    Task<TimeEntryInputModel> GetInputModelAsync(Guid userId, DateOnly workDate, CancellationToken cancellationToken);

    Task SaveAsync(Guid userId, TimeEntryInputModel input, CancellationToken cancellationToken);

    Task<IReadOnlyList<TimeEntryAudit>> GetAuditHistoryAsync(Guid userId, DateOnly workDate, CancellationToken cancellationToken);
}