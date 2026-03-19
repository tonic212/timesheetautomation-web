namespace TimesheetAutomation.Web.Models;

public sealed class FortnightDayViewModel
{
    public DateOnly WorkDate { get; set; }

    public string DayName { get; set; } = string.Empty;

    public bool HasEntry { get; set; }

    public TimeOnly? StartTime { get; set; }

    public TimeOnly? FinishTime { get; set; }

    public int? MealBreakMinutes { get; set; }

    public string StatusText => HasEntry ? "Saved" : "Missing";

    public string StartHourText => StartTime.HasValue ? ToHour12(StartTime.Value).ToString() : string.Empty;

    public string StartMinuteText => StartTime.HasValue ? StartTime.Value.Minute.ToString("00") : string.Empty;

    public string StartPeriodText => StartTime.HasValue ? ToPeriod(StartTime.Value) : string.Empty;

    public string FinishHourText => FinishTime.HasValue ? ToHour12(FinishTime.Value).ToString() : string.Empty;

    public string FinishMinuteText => FinishTime.HasValue ? FinishTime.Value.Minute.ToString("00") : string.Empty;

    public string FinishPeriodText => FinishTime.HasValue ? ToPeriod(FinishTime.Value) : string.Empty;

    private static int ToHour12(TimeOnly time)
    {
        int hour12 = time.Hour % 12;
        return hour12 == 0 ? 12 : hour12;
    }

    private static string ToPeriod(TimeOnly time)
    {
        return time.Hour >= 12 ? "PM" : "AM";
    }
}