using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.IO;
using TimesheetAutomation.Web.Data;
using TimesheetAutomation.Web.Options;
using TimesheetAutomation.Web.Security;
using TimesheetAutomation.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/");
    options.Conventions.AllowAnonymousToPage("/Login");
    options.Conventions.AllowAnonymousToPage("/Register");
    options.Conventions.AllowAnonymousToPage("/ForgotPassword");
    options.Conventions.AllowAnonymousToPage("/ResetPassword");
    options.Conventions.AllowAnonymousToPage("/AccessDenied");
});

builder.Services.Configure<ExcelExportOptions>(builder.Configuration.GetSection("ExcelExport"));
builder.Services.Configure<PayPeriodOptions>(builder.Configuration.GetSection("PayPeriod"));
builder.Services.Configure<AuthSecurityOptions>(builder.Configuration.GetSection("AuthSecurity"));
builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection("Smtp"));

string authConnectionString = builder.Configuration.GetConnectionString("AuthConnection")
    ?? "Data Source=docker-data/App_Data/auth.db";

string appConnectionString = builder.Configuration.GetConnectionString("AppConnection")
    ?? "Data Source=docker-data/App_Data/app.db";

string resolvedAuthConnectionString = ResolveSqliteConnectionString(
    authConnectionString,
    builder.Environment.ContentRootPath);

string resolvedAppConnectionString = ResolveSqliteConnectionString(
    appConnectionString,
    builder.Environment.ContentRootPath);

builder.Services.AddDbContext<AuthDbContext>(options =>
{
    options.UseSqlite(resolvedAuthConnectionString);
});

builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseSqlite(resolvedAppConnectionString);
});

builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo("/app/docker-data/DataProtectionKeys"));

builder.Services.AddScoped<ICurrentUserAccessor, CurrentUserAccessor>();
builder.Services.AddScoped<IUserAuthService, UserAuthService>();
builder.Services.AddScoped<ITimeEntryService, TimeEntryService>();
builder.Services.AddScoped<IFortnightSummaryService, FortnightSummaryService>();
builder.Services.AddScoped<IFortnightExportService, FortnightExportService>();
builder.Services.AddScoped<ITilLedgerImportService, TilLedgerImportService>();
builder.Services.AddScoped<ITwoFactorService, TwoFactorService>();
builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Login";
        options.AccessDeniedPath = "/AccessDenied";
        options.Cookie.Name = "TimesheetAutomation.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
    });

builder.Services.AddAuthorization();
builder.Services.AddAntiforgery();

var app = builder.Build();

using (IServiceScope scope = app.Services.CreateScope())
{
    AuthDbContext authDbContext = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
    authDbContext.Database.Migrate();

    AppDbContext appDbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    appDbContext.Database.Migrate();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();

app.Use(async (context, next) =>
{
    if (context.User.Identity?.IsAuthenticated == true)
    {
        AuthSecurityOptions authSecurity = context.RequestServices
            .GetRequiredService<IOptions<AuthSecurityOptions>>().Value;

        if (authSecurity.RequireTwoFactorEnrollment)
        {
            string path = context.Request.Path.Value ?? string.Empty;

            bool isAllowedPath =
                path.StartsWith("/Welcome", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("/EnableAuthenticator", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("/Login", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("/LoginWith2fa", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("/Logout", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("/ForgotPassword", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("/ResetPassword", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("/AccessDenied", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("/css", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("/js", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("/lib", StringComparison.OrdinalIgnoreCase);

            if (!isAllowedPath)
            {
                IUserAuthService userAuthService = context.RequestServices.GetRequiredService<IUserAuthService>();
                Guid userId = Guid.Parse(context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
                var user = await userAuthService.GetByIdAsync(userId, context.RequestAborted);

                if (user is not null && !user.IsTwoFactorEnabled)
                {
                    context.Response.Redirect("/EnableAuthenticator");
                    return;
                }
            }
        }
    }

    await next();
});

app.UseAuthorization();
app.MapRazorPages();

app.Run();

static string ResolveSqliteConnectionString(string connectionString, string contentRootPath)
{
    const string prefix = "Data Source=";

    if (!connectionString.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
    {
        return connectionString;
    }

    string rawPath = connectionString[prefix.Length..].Trim().Trim('"');

    if (string.IsNullOrWhiteSpace(rawPath))
    {
        throw new InvalidOperationException("The SQLite connection string Data Source is empty.");
    }

    string fullPath = Path.IsPathRooted(rawPath)
        ? rawPath
        : Path.Combine(contentRootPath, rawPath);

    string? directory = Path.GetDirectoryName(fullPath);
    if (string.IsNullOrWhiteSpace(directory))
    {
        throw new InvalidOperationException($"Could not determine the SQLite directory for path '{fullPath}'.");
    }

    Directory.CreateDirectory(directory);

    return $"{prefix}{fullPath}";
}