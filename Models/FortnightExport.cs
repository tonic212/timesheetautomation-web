using System.ComponentModel.DataAnnotations;

namespace TimesheetAutomation.Web.Models;

public sealed class FortnightExport
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid UserId { get; set; }

    [Required]
    public DateOnly PeriodStartDate { get; set; }

    [Required]
    public DateOnly PeriodEndDate { get; set; }

    [Required]
    [MaxLength(260)]
    public string FilePath { get; set; } = string.Empty;

    [Required]
    [MaxLength(260)]
    public string FileName { get; set; } = string.Empty;

    [Required]
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}