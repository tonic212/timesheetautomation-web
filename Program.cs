using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.EntityFrameworkCore;
using TimesheetAutomation.Web.Data;
using TimesheetAutomation.Web.Models;
using TimesheetAutomation.Web.Options;
using TimesheetAutomation.Web.Security;
using TimesheetAutomation.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/");
    options.Conventions.AllowAnonymousToPage("/Login");
    options.Conventions.AllowAnonymousToPage("/AccessDenied");
});

builder.Services.Configure<ExcelExportOptions>(builder.Configuration.GetSection("ExcelExport"));
builder.Services.Configure<PayPeriodOptions>(builder.Configuration.GetSection("PayPeriod"));

builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection"));
});

builder.Services.AddScoped<ICurrentUserAccessor, CurrentUserAccessor>();
builder.Services.AddScoped<IGoogleUserProvisioningService, GoogleUserProvisioningService>();
builder.Services.AddScoped<ITimeEntryService, TimeEntryService>();
builder.Services.AddScoped<IFortnightSummaryService, FortnightSummaryService>();
builder.Services.AddScoped<IFortnightExportService, FortnightExportService>();

builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = GoogleDefaults.AuthenticationScheme;
})
.AddCookie(options =>
{
    options.LoginPath = "/Login";
    options.AccessDeniedPath = "/AccessDenied";
    options.Cookie.Name = "TimesheetAutomation.Auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
})
.AddGoogle(options =>
{
    options.ClientId = builder.Configuration["Authentication:Google:ClientId"] ?? string.Empty;
    options.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"] ?? string.Empty;
    options.CallbackPath = "/signin-google";

    options.Scope.Clear();
    options.Scope.Add("openid");
    options.Scope.Add("profile");
    options.Scope.Add("email");

    options.Events.OnCreatingTicket = async context =>
    {
        string? email = context.Identity?.FindFirst(ClaimTypes.Email)?.Value
            ?? context.User.GetProperty("email").GetString();

        string? name = context.Identity?.FindFirst(ClaimTypes.Name)?.Value
            ?? context.User.GetProperty("name").GetString();

        string? googleSubject = context.User.GetProperty("sub").GetString();

        string hostedDomain = string.Empty;
        if (context.User.TryGetProperty("hd", out var hdProperty))
        {
            hostedDomain = hdProperty.GetString() ?? string.Empty;
        }

        if (string.IsNullOrWhiteSpace(email) ||
            string.IsNullOrWhiteSpace(name) ||
            string.IsNullOrWhiteSpace(googleSubject))
        {
            throw new InvalidOperationException("Google sign-in did not return required claims.");
        }

        if (!string.Equals(hostedDomain, "chemwatch.net", StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("Only chemwatch.net accounts are allowed.");
        }

        if (!email.EndsWith("@chemwatch.net", StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("Only chemwatch.net accounts are allowed.");
        }

        using IServiceScope scope = context.HttpContext.RequestServices.CreateScope();
        IGoogleUserProvisioningService provisioningService =
            scope.ServiceProvider.GetRequiredService<IGoogleUserProvisioningService>();

        ApplicationUser applicationUser = await provisioningService.GetOrCreateUserAsync(
            email,
            name,
            hostedDomain,
            googleSubject,
            context.HttpContext.RequestAborted);

        List<Claim> claims =
        [
            new Claim(ClaimTypes.NameIdentifier, applicationUser.Id.ToString()),
            new Claim(ClaimTypes.Name, applicationUser.DisplayName),
            new Claim(ClaimTypes.Email, applicationUser.Email),
            new Claim("hd", applicationUser.HostedDomain)
        ];

        if (applicationUser.IsAdmin)
        {
            claims.Add(new Claim(ClaimTypes.Role, "Admin"));
        }

        ClaimsIdentity identity = new(
            claims,
            CookieAuthenticationDefaults.AuthenticationScheme);

        context.Principal = new ClaimsPrincipal(identity);
    };

    options.Events.OnRemoteFailure = context =>
    {
        context.Response.Redirect("/AccessDenied");
        context.HandleResponse();
        return Task.CompletedTask;
    };
});

builder.Services.AddAuthorization();
builder.Services.AddAntiforgery();

var app = builder.Build();

using (IServiceScope scope = app.Services.CreateScope())
{
    ApplicationDbContext dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    dbContext.Database.Migrate();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();

app.Run();