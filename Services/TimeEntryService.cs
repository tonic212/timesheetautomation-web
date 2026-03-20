using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TimesheetAutomation.Web.Data;
using TimesheetAutomation.Web.Models;
using TimesheetAutomation.Web.Options;

namespace TimesheetAutomation.Web.Services;

public sealed class TimeEntryService : ITimeEntryService
{
    private readonly AppDbContext _dbContext;
    private readonly PayPeriodOptions _payPeriodOptions;

    public TimeEntryService(
        AppDbContext dbContext,
        IOptions<PayPeriodOptions> payPeriodOptions)
    {
        _dbContext = dbContext;
        _payPeriodOptions = payPeriodOptions.Value;
    }

    public async Task<DailyTimeEntry?> GetByUserAndDateAsync(Guid userId, DateOnly workDate, CancellationToken cancellationToken)
    {
        return await _dbContext.DailyTimeEntries
            .SingleOrDefaultAsync(
                x => x.UserId == userId && x.WorkDate == workDate,
                cancellationToken);
    }

    public async Task<TimeEntryInputModel> GetInputModelAsync(Guid userId, DateOnly workDate, CancellationToken cancellationToken)
    {
        DailyTimeEntry? entry = await GetByUserAndDateAsync(userId, workDate, cancellationToken);

        if (entry is null)
        {
            return new TimeEntryInputModel
            {
                WorkDate = workDate,
                PublicHolidayWorked = true
            };
        }

        return new TimeEntryInputModel
        {
            WorkDate = entry.WorkDate,
            StartTime = entry.StartTime,
            FinishTime = entry.FinishTime,
            MealBreakMinutes = entry.MealBreakMinutes,
            IsPublicHoliday = entry.IsPublicHoliday,
            PublicHolidayWorked = entry.IsPublicHoliday
                ? (entry.StartTime.HasValue && entry.FinishTime.HasValue)
                : true,
            AnnualLeaveHours = entry.AnnualLeaveHours,
            SickLeaveHours = entry.SickLeaveHours,
            LongServiceLeaveHours = entry.LongServiceLeaveHours,
            TimeInLieuTakenHours = entry.TimeInLieuTakenHours,
            Notes = entry.Notes
        };
    }

    public async Task SaveAsync(Guid userId, TimeEntryInputModel input, CancellationToken cancellationToken)
    {
        ValidateBusinessRules(input);

        decimal netWorkedHours = CalculateNetWorkedHours(input.StartTime, input.FinishTime, input.MealBreakMinutes);
        decimal calculatedPublicHolidayHours = CalculatePublicHolidayHours(
            input.IsPublicHoliday,
            input.PublicHolidayWorked,
            input.StartTime,
            input.FinishTime,
            netWorkedHours);

        decimal calculatedTilAccruedHours = CalculateTilAccruedHours(
            input.IsPublicHoliday,
            input.PublicHolidayWorked,
            input.StartTime,
            input.FinishTime,
            netWorkedHours);

        DailyTimeEntry? existingEntry = await _dbContext.DailyTimeEntries
            .Include(x => x.TilLedgerEntries)
            .SingleOrDefaultAsync(
                x => x.UserId == userId && x.WorkDate == input.WorkDate,
                cancellationToken);

        if (existingEntry is null)
        {
            DailyTimeEntry newEntry = new()
            {
                UserId = userId,
                WorkDate = input.WorkDate,
                StartTime = input.StartTime,
                FinishTime = input.FinishTime,
                MealBreakMinutes = input.MealBreakMinutes,
                IsPublicHoliday = input.IsPublicHoliday,
                PublicHolidayHours = calculatedPublicHolidayHours,
                TimeInLieuAccruedHours = calculatedTilAccruedHours,
                AnnualLeaveHours = input.AnnualLeaveHours,
                SickLeaveHours = input.SickLeaveHours,
                LongServiceLeaveHours = input.LongServiceLeaveHours,
                TimeInLieuTakenHours = input.TimeInLieuTakenHours,
                Notes = NormalizeNotes(input.Notes),
                CreatedUtc = DateTime.UtcNow,
                LastModifiedUtc = DateTime.UtcNow
            };

            _dbContext.DailyTimeEntries.Add(newEntry);
            await _dbContext.SaveChangesAsync(cancellationToken);

            await SyncTilLedgerAsync(newEntry, cancellationToken);

            List<TimeEntryAudit> createAudits = BuildCreateAudits(userId, newEntry);
            if (createAudits.Count > 0)
            {
                _dbContext.TimeEntryAudits.AddRange(createAudits);
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            return;
        }

        if (existingEntry.IsLocked)
        {
            throw new InvalidOperationException("This timesheet entry is locked and cannot be edited.");
        }

        List<TimeEntryAudit> audits = new();

        AddAuditIfChanged(audits, userId, existingEntry.Id, "StartTime", existingEntry.StartTime, input.StartTime);
        AddAuditIfChanged(audits, userId, existingEntry.Id, "FinishTime", existingEntry.FinishTime, input.FinishTime);
        AddAuditIfChanged(audits, userId, existingEntry.Id, "MealBreakMinutes", existingEntry.MealBreakMinutes, input.MealBreakMinutes);
        AddAuditIfChanged(audits, userId, existingEntry.Id, "IsPublicHoliday", existingEntry.IsPublicHoliday, input.IsPublicHoliday);
        AddAuditIfChanged(audits, userId, existingEntry.Id, "PublicHolidayHours", existingEntry.PublicHolidayHours, calculatedPublicHolidayHours);
        AddAuditIfChanged(audits, userId, existingEntry.Id, "TimeInLieuAccruedHours", existingEntry.TimeInLieuAccruedHours, calculatedTilAccruedHours);
        AddAuditIfChanged(audits, userId, existingEntry.Id, "AnnualLeaveHours", existingEntry.AnnualLeaveHours, input.AnnualLeaveHours);
        AddAuditIfChanged(audits, userId, existingEntry.Id, "SickLeaveHours", existingEntry.SickLeaveHours, input.SickLeaveHours);
        AddAuditIfChanged(audits, userId, existingEntry.Id, "LongServiceLeaveHours", existingEntry.LongServiceLeaveHours, input.LongServiceLeaveHours);
        AddAuditIfChanged(audits, userId, existingEntry.Id, "TimeInLieuTakenHours", existingEntry.TimeInLieuTakenHours, input.TimeInLieuTakenHours);
        AddAuditIfChanged(audits, userId, existingEntry.Id, "Notes", existingEntry.Notes, NormalizeNotes(input.Notes));

        existingEntry.StartTime = input.StartTime;
        existingEntry.FinishTime = input.FinishTime;
        existingEntry.MealBreakMinutes = input.MealBreakMinutes;
        existingEntry.IsPublicHoliday = input.IsPublicHoliday;
        existingEntry.PublicHolidayHours = calculatedPublicHolidayHours;
        existingEntry.TimeInLieuAccruedHours = calculatedTilAccruedHours;
        existingEntry.AnnualLeaveHours = input.AnnualLeaveHours;
        existingEntry.SickLeaveHours = input.SickLeaveHours;
        existingEntry.LongServiceLeaveHours = input.LongServiceLeaveHours;
        existingEntry.TimeInLieuTakenHours = input.TimeInLieuTakenHours;
        existingEntry.Notes = NormalizeNotes(input.Notes);
        existingEntry.LastModifiedUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        await SyncTilLedgerAsync(existingEntry, cancellationToken);

        if (audits.Count > 0)
        {
            _dbContext.TimeEntryAudits.AddRange(audits);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<IReadOnlyList<TimeEntryAudit>> GetAuditHistoryAsync(Guid userId, DateOnly workDate, CancellationToken cancellationToken)
    {
        DailyTimeEntry? entry = await _dbContext.DailyTimeEntries
            .SingleOrDefaultAsync(
                x => x.UserId == userId && x.WorkDate == workDate,
                cancellationToken);

        if (entry is null)
        {
            return Array.Empty<TimeEntryAudit>();
        }

        return await _dbContext.TimeEntryAudits
            .Where(x => x.UserId == userId && x.DailyTimeEntryId == entry.Id)
            .OrderByDescending(x => x.ChangedUtc)
            .ToListAsync(cancellationToken);
    }

    private async Task SyncTilLedgerAsync(DailyTimeEntry entry, CancellationToken cancellationToken)
    {
        List<TilLedgerEntry> existingLedgerRows = await _dbContext.TilLedgerEntries
            .Where(x => x.SourceKind == "DailyEntry" && x.SourceDailyTimeEntryId == entry.Id)
            .ToListAsync(cancellationToken);

        TilLedgerEntry? accruedRow = existingLedgerRows
            .SingleOrDefault(x => string.Equals(x.EntryType, "Accrued", StringComparison.OrdinalIgnoreCase));

        TilLedgerEntry? takenRow = existingLedgerRows
            .SingleOrDefault(x => string.Equals(x.EntryType, "Taken", StringComparison.OrdinalIgnoreCase));

        string? description = NormalizeNotes(entry.Notes);

        if (entry.TimeInLieuAccruedHours > 0)
        {
            if (accruedRow is null)
            {
                accruedRow = new TilLedgerEntry
                {
                    UserId = entry.UserId,
                    SourceDailyTimeEntryId = entry.Id,
                    SourceKind = "DailyEntry",
                    WorkDate = entry.WorkDate,
                    EntryType = "Accrued",
                    Hours = entry.TimeInLieuAccruedHours,
                    Description = description,
                    CreatedUtc = DateTime.UtcNow,
                    LastModifiedUtc = DateTime.UtcNow
                };

                _dbContext.TilLedgerEntries.Add(accruedRow);
            }
            else
            {
                accruedRow.WorkDate = entry.WorkDate;
                accruedRow.Hours = entry.TimeInLieuAccruedHours;
                accruedRow.Description = description;
                accruedRow.LastModifiedUtc = DateTime.UtcNow;
            }
        }
        else if (accruedRow is not null)
        {
            _dbContext.TilLedgerEntries.Remove(accruedRow);
        }

        if (entry.TimeInLieuTakenHours > 0)
        {
            if (takenRow is null)
            {
                takenRow = new TilLedgerEntry
                {
                    UserId = entry.UserId,
                    SourceDailyTimeEntryId = entry.Id,
                    SourceKind = "DailyEntry",
                    WorkDate = entry.WorkDate,
                    EntryType = "Taken",
                    Hours = entry.TimeInLieuTakenHours,
                    Description = description,
                    CreatedUtc = DateTime.UtcNow,
                    LastModifiedUtc = DateTime.UtcNow
                };

                _dbContext.TilLedgerEntries.Add(takenRow);
            }
            else
            {
                takenRow.WorkDate = entry.WorkDate;
                takenRow.Hours = entry.TimeInLieuTakenHours;
                takenRow.Description = description;
                takenRow.LastModifiedUtc = DateTime.UtcNow;
            }
        }
        else if (takenRow is not null)
        {
            _dbContext.TilLedgerEntries.Remove(takenRow);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private void ValidateBusinessRules(TimeEntryInputModel input)
    {
        decimal standardDayHours = _payPeriodOptions.StandardDailyHours;

        bool hasStart = input.StartTime.HasValue;
        bool hasFinish = input.FinishTime.HasValue;
        bool hasWorkedTimes = hasStart && hasFinish;

        bool hasAnnualLeave = input.AnnualLeaveHours > 0;
        bool hasSickLeave = input.SickLeaveHours > 0;
        bool hasLongServiceLeave = input.LongServiceLeaveHours > 0;
        bool hasTilTaken = input.TimeInLieuTakenHours > 0;

        int nonWorkedSourceCount = 0;
        if (input.IsPublicHoliday && !input.PublicHolidayWorked)
        {
            nonWorkedSourceCount++;
        }
        if (hasAnnualLeave)
        {
            nonWorkedSourceCount++;
        }
        if (hasSickLeave)
        {
            nonWorkedSourceCount++;
        }
        if (hasLongServiceLeave)
        {
            nonWorkedSourceCount++;
        }
        if (hasTilTaken && !hasWorkedTimes)
        {
            nonWorkedSourceCount++;
        }

        if (nonWorkedSourceCount > 1)
        {
            throw new InvalidOperationException("Only one non-worked time source can be used for a day.");
        }

        if (input.IsPublicHoliday && input.PublicHolidayWorked && !hasWorkedTimes)
        {
            throw new InvalidOperationException("Worked public holiday entries require both start and finish times.");
        }

        if (input.IsPublicHoliday && !input.PublicHolidayWorked)
        {
            if (hasStart || hasFinish)
            {
                throw new InvalidOperationException("Do not enter start or finish times when the public holiday was not worked.");
            }

            if (input.MealBreakMinutes.GetValueOrDefault() > 0)
            {
                throw new InvalidOperationException("Meal break cannot be entered for a non-worked public holiday.");
            }

            if (hasAnnualLeave || hasSickLeave || hasLongServiceLeave || hasTilTaken)
            {
                throw new InvalidOperationException("Public holiday not worked cannot be combined with other leave or TIL taken hours.");
            }
        }

        if (hasWorkedTimes)
        {
            if (hasAnnualLeave || hasSickLeave || hasLongServiceLeave)
            {
                throw new InvalidOperationException("Worked times cannot be combined with annual leave, sick leave, or long service leave.");
            }

            decimal workedHours = CalculateNetWorkedHours(input.StartTime, input.FinishTime, input.MealBreakMinutes);

            if (hasTilTaken)
            {
                if (workedHours >= standardDayHours)
                {
                    throw new InvalidOperationException("TIL Taken Hours can only be used when worked hours are less than 8.");
                }

                decimal maxTopUp = Math.Round(standardDayHours - workedHours, 2, MidpointRounding.AwayFromZero);

                if (input.TimeInLieuTakenHours > maxTopUp)
                {
                    throw new InvalidOperationException($"TIL Taken Hours cannot exceed {maxTopUp:0.##} for this day.");
                }
            }
        }
    }

    private decimal CalculatePublicHolidayHours(
        bool isPublicHoliday,
        bool publicHolidayWorked,
        TimeOnly? startTime,
        TimeOnly? finishTime,
        decimal netWorkedHours)
    {
        if (!isPublicHoliday)
        {
            return 0m;
        }

        return publicHolidayWorked && startTime.HasValue && finishTime.HasValue
            ? netWorkedHours
            : _payPeriodOptions.StandardDailyHours;
    }

    private static decimal CalculateTilAccruedHours(
        bool isPublicHoliday,
        bool publicHolidayWorked,
        TimeOnly? startTime,
        TimeOnly? finishTime,
        decimal netWorkedHours)
    {
        if (!isPublicHoliday)
        {
            return 0m;
        }

        return publicHolidayWorked && startTime.HasValue && finishTime.HasValue
            ? netWorkedHours
            : 0m;
    }

    private static decimal CalculateNetWorkedHours(TimeOnly? startTime, TimeOnly? finishTime, int? mealBreakMinutes)
    {
        if (!startTime.HasValue || !finishTime.HasValue)
        {
            return 0m;
        }

        TimeSpan duration = finishTime.Value.ToTimeSpan() - startTime.Value.ToTimeSpan();
        if (duration <= TimeSpan.Zero)
        {
            return 0m;
        }

        int breakMinutes = mealBreakMinutes.GetValueOrDefault();
        if (breakMinutes > 0)
        {
            duration -= TimeSpan.FromMinutes(breakMinutes);
        }

        if (duration < TimeSpan.Zero)
        {
            duration = TimeSpan.Zero;
        }

        return Math.Round((decimal)duration.TotalHours, 2, MidpointRounding.AwayFromZero);
    }

    private static string? NormalizeNotes(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static void AddAuditIfChanged<T>(
        List<TimeEntryAudit> audits,
        Guid userId,
        Guid dailyTimeEntryId,
        string fieldName,
        T oldValue,
        T newValue)
    {
        string? oldText = ConvertToAuditString(oldValue);
        string? newText = ConvertToAuditString(newValue);

        if (string.Equals(oldText, newText, StringComparison.Ordinal))
        {
            return;
        }

        audits.Add(new TimeEntryAudit
        {
            DailyTimeEntryId = dailyTimeEntryId,
            UserId = userId,
            FieldName = fieldName,
            OldValue = oldText,
            NewValue = newText,
            ChangedUtc = DateTime.UtcNow
        });
    }

    private static List<TimeEntryAudit> BuildCreateAudits(Guid userId, DailyTimeEntry entry)
    {
        List<TimeEntryAudit> audits = new();

        AddCreateAuditIfValuePresent(audits, userId, entry.Id, "StartTime", entry.StartTime);
        AddCreateAuditIfValuePresent(audits, userId, entry.Id, "FinishTime", entry.FinishTime);
        AddCreateAuditIfValuePresent(audits, userId, entry.Id, "MealBreakMinutes", entry.MealBreakMinutes);
        AddCreateAuditIfValuePresent(audits, userId, entry.Id, "IsPublicHoliday", entry.IsPublicHoliday);
        AddCreateAuditIfValuePresent(audits, userId, entry.Id, "PublicHolidayHours", entry.PublicHolidayHours);
        AddCreateAuditIfValuePresent(audits, userId, entry.Id, "TimeInLieuAccruedHours", entry.TimeInLieuAccruedHours);
        AddCreateAuditIfValuePresent(audits, userId, entry.Id, "AnnualLeaveHours", entry.AnnualLeaveHours);
        AddCreateAuditIfValuePresent(audits, userId, entry.Id, "SickLeaveHours", entry.SickLeaveHours);
        AddCreateAuditIfValuePresent(audits, userId, entry.Id, "LongServiceLeaveHours", entry.LongServiceLeaveHours);
        AddCreateAuditIfValuePresent(audits, userId, entry.Id, "TimeInLieuTakenHours", entry.TimeInLieuTakenHours);
        AddCreateAuditIfValuePresent(audits, userId, entry.Id, "Notes", entry.Notes);

        return audits;
    }

    private static void AddCreateAuditIfValuePresent<T>(
        List<TimeEntryAudit> audits,
        Guid userId,
        Guid dailyTimeEntryId,
        string fieldName,
        T value)
    {
        string? text = ConvertToAuditString(value);

        if (string.IsNullOrWhiteSpace(text) || text == "0" || text == "False")
        {
            return;
        }

        audits.Add(new TimeEntryAudit
        {
            DailyTimeEntryId = dailyTimeEntryId,
            UserId = userId,
            FieldName = fieldName,
            OldValue = null,
            NewValue = text,
            ChangedUtc = DateTime.UtcNow
        });
    }

    private static string? ConvertToAuditString<T>(T value)
    {
        if (value is null)
        {
            return null;
        }

        return value switch
        {
            TimeOnly time => time.ToString("HH:mm"),
            DateOnly date => date.ToString("yyyy-MM-dd"),
            decimal number => number.ToString("0.##"),
            int minutes => minutes.ToString(),
            bool boolean => boolean ? "True" : "False",
            string text => string.IsNullOrWhiteSpace(text) ? null : text.Trim(),
            _ => value.ToString()
        };
    }
}