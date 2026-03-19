using System.ComponentModel.DataAnnotations;

namespace TimesheetAutomation.Web.Models;

public sealed class TimeEntryAudit
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid DailyTimeEntryId { get; set; }

    [Required]
    public Guid UserId { get; set; }

    [Required]
    [MaxLength(100)]
    public string FieldName { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? OldValue { get; set; }

    [MaxLength(200)]
    public string? NewValue { get; set; }

    [Required]
    public DateTime ChangedUtc { get; set; } = DateTime.UtcNow;
}