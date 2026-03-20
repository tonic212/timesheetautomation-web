using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Options;
using TimesheetAutomation.Web.Models;
using TimesheetAutomation.Web.Options;
using TimesheetAutomation.Web.Security;
using TimesheetAutomation.Web.Services;

namespace TimesheetAutomation.Web.Pages;

[Authorize]
public sealed class EntryModel : PageModel
{
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly ITimeEntryService _timeEntryService;
    private readonly IFortnightSummaryService _fortnightSummaryService;
    private readonly PayPeriodOptions _payPeriodOptions;

    public EntryModel(
        ICurrentUserAccessor currentUserAccessor,
        ITimeEntryService timeEntryService,
        IFortnightSummaryService fortnightSummaryService,
        IOptions<PayPeriodOptions> payPeriodOptions)
    {
        _currentUserAccessor = currentUserAccessor;
        _timeEntryService = timeEntryService;
        _fortnightSummaryService = fortnightSummaryService;
        _payPeriodOptions = payPeriodOptions.Value;
    }

    [BindProperty]
    public TimeEntryInputModel Input { get; set; } = new();

    public IReadOnlyList<TimeEntryAudit> AuditHistory { get; private set; } = Array.Empty<TimeEntryAudit>();

    public IReadOnlyList<SelectListItem> HourOptions { get; private set; } = Array.Empty<SelectListItem>();

    public IReadOnlyList<SelectListItem> MinuteOptions { get; private set; } = Array.Empty<SelectListItem>();

    public IReadOnlyList<SelectListItem> PeriodOptions { get; private set; } = Array.Empty<SelectListItem>();

    public IReadOnlyList<SelectListItem> MealBreakOptions { get; private set; } = Array.Empty<SelectListItem>();

    public DateOnly MinEditableDate { get; private set; }

    public DateOnly MaxEditableDate { get; private set; }

    public decimal StandardDayHours => _payPeriodOptions.StandardDailyHours;

    public async Task<IActionResult> OnGetAsync(string? date, CancellationToken cancellationToken)
    {
        (MinEditableDate, MaxEditableDate) = GetEditableRange();

        DateOnly requestedDate;
        if (string.IsNullOrWhiteSpace(date))
        {
            requestedDate = MaxEditableDate;
        }
        else if (!DateOnly.TryParse(date, out requestedDate))
        {
            return RedirectToPage("/Entry", new { date = MaxEditableDate.ToString("yyyy-MM-dd") });
        }

        if (requestedDate < MinEditableDate)
        {
            return RedirectToPage("/Entry", new { date = MinEditableDate.ToString("yyyy-MM-dd") });
        }

        if (requestedDate > MaxEditableDate)
        {
            return RedirectToPage("/Entry", new { date = MaxEditableDate.ToString("yyyy-MM-dd") });
        }

        Guid userId = _currentUserAccessor.GetRequiredUserId(User);

        Input = await _timeEntryService.GetInputModelAsync(userId, requestedDate, cancellationToken);

        if (!Input.IsPublicHoliday)
        {
            Input.PublicHolidayWorked = true;
        }

        ApplyTimeDefaultsAndSelections();
        BuildDropdowns();

        AuditHistory = await _timeEntryService.GetAuditHistoryAsync(userId, requestedDate, cancellationToken);

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        (MinEditableDate, MaxEditableDate) = GetEditableRange();

        if (Input.WorkDate < MinEditableDate || Input.WorkDate > MaxEditableDate)
        {
            ModelState.AddModelError(string.Empty, $"You can only edit dates from {MinEditableDate:dd/MM/yyyy} to {MaxEditableDate:dd/MM/yyyy}.");
        }

        BuildDropdowns();

        if (Input.IsPublicHoliday && !Input.PublicHolidayWorked)
        {
            ClearTimeInputs();
        }

        ConvertDropdownValuesToTimes();
        ValidateInput();

        Guid userId = _currentUserAccessor.GetRequiredUserId(User);

        if (!ModelState.IsValid)
        {
            AuditHistory = await _timeEntryService.GetAuditHistoryAsync(userId, Input.WorkDate, cancellationToken);
            return Page();
        }

        try
        {
            await _timeEntryService.SaveAsync(userId, Input, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            AuditHistory = await _timeEntryService.GetAuditHistoryAsync(userId, Input.WorkDate, cancellationToken);
            return Page();
        }

        TempData["StatusMessage"] = $"Timesheet for {Input.WorkDate:dd/MM/yyyy} saved successfully.";
        return RedirectToPage("/Index");
    }

    private (DateOnly MinDate, DateOnly MaxDate) GetEditableRange()
    {
        DateOnly today = DateOnly.FromDateTime(DateTime.Today);
        (DateOnly startOfFortnight, _) = _fortnightSummaryService.GetCurrentFortnightRange(today);

        return (startOfFortnight, today);
    }

    private void ApplyTimeDefaultsAndSelections()
    {
        if (Input.IsPublicHoliday && !Input.PublicHolidayWorked)
        {
            Input.StartHour = null;
            Input.StartMinute = null;
            Input.StartPeriod = null;
            Input.FinishHour = null;
            Input.FinishMinute = null;
            Input.FinishPeriod = null;
            Input.MealBreakMinutes = null;
            return;
        }

        if (Input.StartTime.HasValue)
        {
            SetDropdownsFromTime(Input.StartTime.Value, true);
        }
        else
        {
            Input.StartHour = 9;
            Input.StartMinute = 0;
            Input.StartPeriod = "AM";
        }

        if (Input.FinishTime.HasValue)
        {
            SetDropdownsFromTime(Input.FinishTime.Value, false);
        }
        else
        {
            Input.FinishHour = 5;
            Input.FinishMinute = 30;
            Input.FinishPeriod = "PM";
        }

        if (!Input.MealBreakMinutes.HasValue)
        {
            Input.MealBreakMinutes = 30;
        }
    }

    private void SetDropdownsFromTime(TimeOnly value, bool isStart)
    {
        int hour24 = value.Hour;
        int minute = value.Minute;

        string period = hour24 >= 12 ? "PM" : "AM";
        int hour12 = hour24 % 12;
        if (hour12 == 0)
        {
            hour12 = 12;
        }

        if (isStart)
        {
            Input.StartHour = hour12;
            Input.StartMinute = minute;
            Input.StartPeriod = period;
        }
        else
        {
            Input.FinishHour = hour12;
            Input.FinishMinute = minute;
            Input.FinishPeriod = period;
        }
    }

    private void ConvertDropdownValuesToTimes()
    {
        Input.StartTime = BuildTimeOnly(Input.StartHour, Input.StartMinute, Input.StartPeriod, "Input.StartTime");
        Input.FinishTime = BuildTimeOnly(Input.FinishHour, Input.FinishMinute, Input.FinishPeriod, "Input.FinishTime");
    }

    private void ClearTimeInputs()
    {
        Input.StartHour = null;
        Input.StartMinute = null;
        Input.StartPeriod = null;
        Input.FinishHour = null;
        Input.FinishMinute = null;
        Input.FinishPeriod = null;
        Input.StartTime = null;
        Input.FinishTime = null;
        Input.MealBreakMinutes = null;
    }

    private TimeOnly? BuildTimeOnly(int? hour12, int? minute, string? period, string fieldKey)
    {
        bool hasAnyValue = hour12.HasValue || minute.HasValue || !string.IsNullOrWhiteSpace(period);

        if (!hasAnyValue)
        {
            return null;
        }

        if (!hour12.HasValue)
        {
            ModelState.AddModelError(fieldKey, "Hour is required.");
            return null;
        }

        if (!minute.HasValue)
        {
            ModelState.AddModelError(fieldKey, "Minute is required.");
            return null;
        }

        if (string.IsNullOrWhiteSpace(period))
        {
            ModelState.AddModelError(fieldKey, "AM/PM is required.");
            return null;
        }

        if (hour12.Value < 1 || hour12.Value > 12)
        {
            ModelState.AddModelError(fieldKey, "Hour must be between 1 and 12.");
            return null;
        }

        if (minute.Value < 0 || minute.Value > 59)
        {
            ModelState.AddModelError(fieldKey, "Minute must be between 0 and 59.");
            return null;
        }

        string normalizedPeriod = period.Trim().ToUpperInvariant();
        if (normalizedPeriod != "AM" && normalizedPeriod != "PM")
        {
            ModelState.AddModelError(fieldKey, "AM/PM selection is invalid.");
            return null;
        }

        int hour24 = hour12.Value % 12;
        if (normalizedPeriod == "PM")
        {
            hour24 += 12;
        }

        return new TimeOnly(hour24, minute.Value);
    }

    private void ValidateInput()
    {
        bool hasWorkedTimes = Input.StartTime.HasValue || Input.FinishTime.HasValue;

        if (Input.StartTime.HasValue && !Input.FinishTime.HasValue)
        {
            ModelState.AddModelError("Input.FinishTime", "Finish time is required when start time is entered.");
        }

        if (!Input.StartTime.HasValue && Input.FinishTime.HasValue)
        {
            ModelState.AddModelError("Input.StartTime", "Start time is required when finish time is entered.");
        }

        if (Input.MealBreakMinutes.HasValue && Input.MealBreakMinutes.Value > 0 && !hasWorkedTimes)
        {
            ModelState.AddModelError("Input.MealBreakMinutes", "Meal break can only be entered when start and finish times are provided.");
        }

        if (Input.StartTime.HasValue && Input.FinishTime.HasValue)
        {
            if (Input.FinishTime.Value <= Input.StartTime.Value)
            {
                ModelState.AddModelError("Input.FinishTime", "Finish time must be later than start time.");
            }

            if (Input.MealBreakMinutes.HasValue && Input.MealBreakMinutes.Value > 0)
            {
                double workedMinutes = (Input.FinishTime.Value.ToTimeSpan() - Input.StartTime.Value.ToTimeSpan()).TotalMinutes;
                if (Input.MealBreakMinutes.Value >= workedMinutes)
                {
                    ModelState.AddModelError("Input.MealBreakMinutes", "Meal break must be shorter than the worked duration.");
                }
            }
        }

        if (Input.IsPublicHoliday && Input.PublicHolidayWorked)
        {
            if (!Input.StartTime.HasValue)
            {
                ModelState.AddModelError("Input.StartTime", "Start time is required if you worked the public holiday.");
            }

            if (!Input.FinishTime.HasValue)
            {
                ModelState.AddModelError("Input.FinishTime", "Finish time is required if you worked the public holiday.");
            }
        }

        if (Input.TimeInLieuTakenHours > 0 && string.IsNullOrWhiteSpace(Input.Notes))
        {
            ModelState.AddModelError("Input.Notes", "Comments are required when TIL is taken.");
        }

        if (Input.IsPublicHoliday && string.IsNullOrWhiteSpace(Input.Notes))
        {
            ModelState.AddModelError("Input.Notes", "Comments are required for public holiday entries.");
        }
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
            new SelectListItem("Select break", ""),
            new SelectListItem("15 min", "15"),
            new SelectListItem("30 min", "30"),
            new SelectListItem("45 min", "45"),
            new SelectListItem("1 hr", "60"),
            new SelectListItem("1 hr 15 min", "75"),
            new SelectListItem("1 hr 30 min", "90")
        ];
    }
}