using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TimesheetAutomation.Web.Data;
using TimesheetAutomation.Web.Models;
using TimesheetAutomation.Web.Options;

namespace TimesheetAutomation.Web.Services;

public sealed class FortnightSummaryService : IFortnightSummaryService
{
    private readonly AppDbContext _dbContext;
    private readonly PayPeriodOptions _payPeriodOptions;

    public FortnightSummaryService(
        AppDbContext dbContext,
        IOptions<PayPeriodOptions> payPeriodOptions)
    {
        _dbContext = dbContext;
        _payPeriodOptions = payPeriodOptions.Value;
    }

    public (DateOnly StartDate, DateOnly EndDate) GetCurrentFortnightRange(DateOnly today)
    {
        if (!DateOnly.TryParse(_payPeriodOptions.FortnightAnchorDate, out DateOnly anchorDate))
        {
            throw new InvalidOperationException("PayPeriod:FortnightAnchorDate is invalid.");
        }

        int daysDifference = today.DayNumber - anchorDate.DayNumber;
        int fortnightOffset = daysDifference >= 0
            ? daysDifference / 14
            : (daysDifference - 13) / 14;

        DateOnly startDate = anchorDate.AddDays(fortnightOffset * 14);
        DateOnly endDate = startDate.AddDays(13);

        return (startDate, endDate);
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

        for (DateOnly date = startDate; date <= endDate; date = date.AddDays(1))
        {
            entryLookup.TryGetValue(date, out DailyTimeEntry? entry);

            result.Add(new FortnightDayViewModel
            {
                WorkDate = date,
                DayName = date.DayOfWeek.ToString(),
                HasEntry = entry is not null,
                StartTime = entry?.StartTime,
                FinishTime = entry?.FinishTime,
                MealBreakMinutes = entry?.MealBreakMinutes
            });
        }

        return result;
    }
}