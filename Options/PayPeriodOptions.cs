namespace TimesheetAutomation.Web.Options;

public sealed class PayPeriodOptions
{
    public string FortnightAnchorDate { get; set; } = "2022-12-29";

    public decimal StandardDailyHours { get; set; } = 8.0m;
}