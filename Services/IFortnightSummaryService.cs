using TimesheetAutomation.Web.Models;

namespace TimesheetAutomation.Web.Services;

public interface IFortnightSummaryService
{
    Task<IReadOnlyList<FortnightDayViewModel>> GetCurrentFortnightAsync(Guid userId, CancellationToken cancellationToken);

    (DateOnly StartDate, DateOnly EndDate) GetCurrentFortnightRange(DateOnly today);
}