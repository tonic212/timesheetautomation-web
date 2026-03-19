using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TimesheetAutomation.Web.Data;
using TimesheetAutomation.Web.Models;
using TimesheetAutomation.Web.Options;

namespace TimesheetAutomation.Web.Services;

public sealed class FortnightSummaryService : IFortnightSummaryService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly PayPeriodOptions _payPeriodOptions;

    public FortnightSummaryService(
        ApplicationDbContext dbContext,
        IOptions<PayPeriodOptions> payPeriodOptions)
    {
        _dbContext = dbContext;
        _payPeriodOptions = payPeriodOptions.Value;
    }

    public async Task<IReadOnlyList<FortnightDayViewModel>> GetCurrentFortnightAsync(Guid userId, CancellationToken cancellationToken)
    {
        DateOnly today = DateOnly.FromDateTime(DateTime.Today);
        (DateOnly startDate, DateOnly endDate) = GetCurrentFortnightRange(today);

        List<DailyTimeEntry> entries = await _dbContext.DailyTimeEntries
            .Where(x => x.UserId == userId && x.WorkDate >= startDate && x.WorkDate <= endDate)
            .OrderBy(x => x.WorkDate)
            .ToListAsync(cancellationToken);

        Dictionary<DateOnly, DailyTimeEntry> entryLookup = entries.ToDictionary(x => x.WorkDate);

        List<FortnightDayViewModel> result = new();

        for (DateOnly current = startDate; current <= endDate; current = current.AddDays(1))
        {
            entryLookup.TryGetValue(current, out DailyTimeEntry? entry);

            result.Add(new FortnightDayViewModel
            {
                WorkDate = current,
                DayName = current.DayOfWeek.ToString(),
                HasEntry = entry is not null,
                StartTime = entry?.StartTime,
                FinishTime = entry?.FinishTime
            });
        }

        return result;
    }

    public (DateOnly StartDate, DateOnly EndDate) GetCurrentFortnightRange(DateOnly today)
    {
        if (!DateOnly.TryParse(_payPeriodOptions.FortnightAnchorDate, out DateOnly anchorDate))
        {
            anchorDate = new DateOnly(2022, 12, 29);
        }

        int daysSinceAnchor = today.DayNumber - anchorDate.DayNumber;
        int fortnightOffset = Math.DivRem(daysSinceAnchor, 14, out int remainder);

        if (daysSinceAnchor < 0 && remainder != 0)
        {
            fortnightOffset--;
        }

        DateOnly startDate = anchorDate.AddDays(fortnightOffset * 14);
        DateOnly endDate = startDate.AddDays(13);

        return (startDate, endDate);
    }
}