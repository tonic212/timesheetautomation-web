using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TimesheetAutomation.Web.Models;

public sealed class DailyTimeEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid UserId { get; set; }

    [Required]
    public DateOnly WorkDate { get; set; }

    public TimeOnly? StartTime { get; set; }

    public TimeOnly? FinishTime { get; set; }

    public int? MealBreakMinutes { get; set; }

    public bool IsPublicHoliday { get; set; }

    [Column(TypeName = "decimal(5,2)")]
    public decimal PublicHolidayHours { get; set; }

    [Column(TypeName = "decimal(5,2)")]
    public decimal TimeInLieuAccruedHours { get; set; }

    [Column(TypeName = "decimal(5,2)")]
    public decimal AnnualLeaveHours { get; set; }

    [Column(TypeName = "decimal(5,2)")]
    public decimal SickLeaveHours { get; set; }

    [Column(TypeName = "decimal(5,2)")]
    public decimal LongServiceLeaveHours { get; set; }

    [Column(TypeName = "decimal(5,2)")]
    public decimal TimeInLieuTakenHours { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    public bool IsLocked { get; set; }

    [Required]
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    [Required]
    public DateTime LastModifiedUtc { get; set; } = DateTime.UtcNow;

    public ICollection<TilLedgerEntry> TilLedgerEntries { get; set; } = new List<TilLedgerEntry>();
}