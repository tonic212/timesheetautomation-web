namespace TimesheetAutomation.Web.Models;

public sealed class PendingTwoFactorLogin
{
    public Guid UserId { get; set; }

    public bool RememberMe { get; set; }
}