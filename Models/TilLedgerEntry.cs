using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TimesheetAutomation.Web.Models;

public sealed class TilLedgerEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid UserId { get; set; }

    public Guid? SourceDailyTimeEntryId { get; set; }

    public DailyTimeEntry? SourceDailyTimeEntry { get; set; }

    [Required]
    [MaxLength(20)]
    public string SourceKind { get; set; } = "DailyEntry";

    [Required]
    public DateOnly WorkDate { get; set; }

    [Required]
    [MaxLength(20)]
    public string EntryType { get; set; } = string.Empty;

    [Column(TypeName = "decimal(5,2)")]
    public decimal Hours { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }

    [Required]
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    [Required]
    public DateTime LastModifiedUtc { get; set; } = DateTime.UtcNow;
}