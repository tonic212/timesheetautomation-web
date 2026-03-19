using System.ComponentModel.DataAnnotations;

namespace TimesheetAutomation.Web.Models;

public sealed class TimeEntryInputModel
{
    [Required]
    public DateOnly WorkDate { get; set; }

    public TimeOnly? StartTime { get; set; }

    public TimeOnly? FinishTime { get; set; }

    [Range(1, 12)]
    public int? StartHour { get; set; }

    [Range(0, 59)]
    public int? StartMinute { get; set; }

    [RegularExpression("AM|PM")]
    public string? StartPeriod { get; set; }

    [Range(1, 12)]
    public int? FinishHour { get; set; }

    [Range(0, 59)]
    public int? FinishMinute { get; set; }

    [RegularExpression("AM|PM")]
    public string? FinishPeriod { get; set; }

    [Range(0, 600)]
    public int? MealBreakMinutes { get; set; }

    public bool IsPublicHoliday { get; set; }

    public bool PublicHolidayWorked { get; set; }

    [Range(0, 24)]
    public decimal AnnualLeaveHours { get; set; }

    [Range(0, 24)]
    public decimal SickLeaveHours { get; set; }

    [Range(0, 24)]
    public decimal LongServiceLeaveHours { get; set; }

    [Range(0, 24)]
    public decimal TimeInLieuTakenHours { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }
}