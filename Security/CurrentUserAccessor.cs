using System.Security.Claims;

namespace TimesheetAutomation.Web.Security;

public interface ICurrentUserAccessor
{
    Guid GetRequiredUserId(ClaimsPrincipal principal);

    string GetRequiredEmail(ClaimsPrincipal principal);

    bool IsAdmin(ClaimsPrincipal principal);
}

public sealed class CurrentUserAccessor : ICurrentUserAccessor
{
    public Guid GetRequiredUserId(ClaimsPrincipal principal)
    {
        string? value = principal.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(value) || !Guid.TryParse(value, out Guid userId))
        {
            throw new InvalidOperationException("Authenticated user id claim is missing.");
        }

        return userId;
    }

    public string GetRequiredEmail(ClaimsPrincipal principal)
    {
        string? email = principal.FindFirstValue(ClaimTypes.Email);

        if (string.IsNullOrWhiteSpace(email))
        {
            throw new InvalidOperationException("Authenticated user email claim is missing.");
        }

        return email;
    }

    public bool IsAdmin(ClaimsPrincipal principal)
    {
        return principal.IsInRole("Admin");
    }
}