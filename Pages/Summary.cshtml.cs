using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using TimesheetAutomation.Web.Models;
using TimesheetAutomation.Web.Security;
using TimesheetAutomation.Web.Services;

namespace TimesheetAutomation.Web.Pages;

[Authorize]
public sealed class SummaryModel : PageModel
{
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IFortnightSummaryService _fortnightSummaryService;
    private readonly ITimeEntryService _timeEntryService;

    public SummaryModel(
        ICurrentUserAccessor currentUserAccessor,
        IFortnightSummaryService fortnightSummaryService,
        ITimeEntryService timeEntryService)
    {
        _currentUserAccessor = currentUserAccessor;
        _fortnightSummaryService = fortnightSummaryService;
        _timeEntryService = timeEntryService;
    }

    public IReadOnlyList<FortnightDayViewModel> Days { get; private set; } = Array.Empty<FortnightDayViewModel>();

    public DateOnly StartDate { get; private set; }

    public DateOnly EndDate { get; private set; }

    public IReadOnlyList<SelectListItem> HourOptions { get; private set; } = Array.Empty<SelectListItem>();

    public IReadOnlyList<SelectListItem> MinuteOptions { get; private set; } = Array.Empty<SelectListItem>();

    public IReadOnlyList<SelectListItem> PeriodOptions { get; private set; } = Array.Empty<SelectListItem>();

    public IReadOnlyList<SelectListItem> MealBreakOptions { get; private set; } = Array.Empty<SelectListItem>();

    [BindProperty]
    public QuickEditInputModel QuickEdit { get; set; } = new();

    [TempData]
    public string? StatusMessage { get; set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        BuildDropdowns();

        Guid userId = _currentUserAccessor.GetRequiredUserId(User);

        (StartDate, EndDate) = _fortnightSummaryService.GetCurrentFortnightRange(DateOnly.FromDateTime(DateTime.Today));
        Days = await _fortnightSummaryService.GetCurrentFortnightAsync(userId, cancellationToken);
    }

    public async Task<IActionResult> OnPostQuickSaveAsync(CancellationToken cancellationToken)
    {
        BuildDropdowns();

        Guid userId = _currentUserAccessor.GetRequiredUserId(User);

        (StartDate, EndDate) = _fortnightSummaryService.GetCurrentFortnightRange(DateOnly.FromDateTime(DateTime.Today));

        if (QuickEdit.WorkDate < StartDate || QuickEdit.WorkDate > EndDate)
        {
            StatusMessage = "Quick edit date is outside the current fortnight.";
            return RedirectToPage("/Summary");
        }

        TimeOnly? startTime = BuildTimeOnly(QuickEdit.StartHour, QuickEdit.StartMinute, QuickEdit.StartPeriod);
        TimeOnly? finishTime = BuildTimeOnly(QuickEdit.FinishHour, QuickEdit.FinishMinute, QuickEdit.FinishPeriod);

        TimeEntryInputModel input = await _timeEntryService.GetInputModelAsync(userId, QuickEdit.WorkDate, cancellationToken);

        input.WorkDate = QuickEdit.WorkDate;
        input.StartTime = startTime;
        input.FinishTime = finishTime;
        input.MealBreakMinutes = QuickEdit.MealBreakMinutes;

        if (!startTime.HasValue && !finishTime.HasValue)
        {
            input.MealBreakMinutes = null;
        }

        await _timeEntryService.SaveAsync(userId, input, cancellationToken);

        StatusMessage = $"Quick update saved for {QuickEdit.WorkDate:dd/MM/yyyy}.";
        return RedirectToPage("/Summary");
    }

    private void BuildDropdowns()
    {
        HourOptions = Enumerable.Range(1, 12)
            .Select(x => new SelectListItem(x.ToString(), x.ToString()))
            .ToList();

        MinuteOptions = Enumerable.Range(0, 60)
            .Select(x => new SelectListItem(x.ToString("00"), x.ToString()))
            .ToList();

        PeriodOptions =
        [
            new SelectListItem("AM", "AM"),
            new SelectListItem("PM", "PM")
        ];

        MealBreakOptions =
        [
            new SelectListItem("", ""),
            new SelectListItem("15 min", "15"),
            new SelectListItem("30 min", "30"),
            new SelectListItem("45 min", "45"),
            new SelectListItem("1 hr", "60"),
            new SelectListItem("1 hr 15 min", "75"),
            new SelectListItem("1 hr 30 min", "90")
        ];
    }

    private static TimeOnly? BuildTimeOnly(string? hourText, string? minuteText, string? period)
    {
        bool hasAnyValue =
            !string.IsNullOrWhiteSpace(hourText) ||
            !string.IsNullOrWhiteSpace(minuteText) ||
            !string.IsNullOrWhiteSpace(period);

        if (!hasAnyValue)
        {
            return null;
        }

        if (!int.TryParse(hourText, out int hour12) || hour12 < 1 || hour12 > 12)
        {
            throw new InvalidOperationException("Invalid quick edit hour value.");
        }

        if (!int.TryParse(minuteText, out int minute) || minute < 0 || minute > 59)
        {
            throw new InvalidOperationException("Invalid quick edit minute value.");
        }

        if (string.IsNullOrWhiteSpace(period))
        {
            throw new InvalidOperationException("Invalid quick edit AM/PM value.");
        }

        string normalizedPeriod = period.Trim().ToUpperInvariant();
        if (normalizedPeriod != "AM" && normalizedPeriod != "PM")
        {
            throw new InvalidOperationException("Invalid quick edit AM/PM value.");
        }

        int hour24 = hour12 % 12;
        if (normalizedPeriod == "PM")
        {
            hour24 += 12;
        }

        return new TimeOnly(hour24, minute);
    }

    public sealed class QuickEditInputModel
    {
        public DateOnly WorkDate { get; set; }

        public string? StartHour { get; set; }

        public string? StartMinute { get; set; }

        public string? StartPeriod { get; set; }

        public string? FinishHour { get; set; }

        public string? FinishMinute { get; set; }

        public string? FinishPeriod { get; set; }

        public int? MealBreakMinutes { get; set; }
    }
}