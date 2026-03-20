using System.ComponentModel.DataAnnotations;

namespace TimesheetAutomation.Web.Models;

public sealed class ApplicationUser
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MaxLength(256)]
    public string NormalizedEmail { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string DisplayName { get; set; } = string.Empty;

    [Required]
    [MaxLength(500)]
    public string PasswordHash { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string HostedDomain { get; set; } = "chemwatch.net";

    [Required]
    [MaxLength(200)]
    public string GoogleSubject { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public bool IsAdmin { get; set; }

    public bool IsTwoFactorEnabled { get; set; }

    [MaxLength(200)]
    public string? AuthenticatorKey { get; set; }

    public string? RecoveryCodes { get; set; }

    [Required]
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    public DateTime? LastLoginUtc { get; set; }
}