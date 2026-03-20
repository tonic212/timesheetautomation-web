using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TimesheetAutomation.Web.Data;
using TimesheetAutomation.Web.Models;
using TimesheetAutomation.Web.Options;

namespace TimesheetAutomation.Web.Services;

public sealed class FortnightExportService : IFortnightExportService
{
    private readonly AppDbContext _dbContext;
    private readonly IFortnightSummaryService _fortnightSummaryService;
    private readonly ExcelExportOptions _options;
    private readonly IWebHostEnvironment _environment;

    public FortnightExportService(
        AppDbContext dbContext,
        IFortnightSummaryService fortnightSummaryService,
        IOptions<ExcelExportOptions> options,
        IWebHostEnvironment environment)
    {
        _dbContext = dbContext;
        _fortnightSummaryService = fortnightSummaryService;
        _options = options.Value;
        _environment = environment;
    }

    public async Task<FortnightExport> GenerateCurrentFortnightAsync(Guid userId, CancellationToken cancellationToken)
    {
        DateOnly today = DateOnly.FromDateTime(DateTime.Today);
        (DateOnly startDate, DateOnly endDate) = _fortnightSummaryService.GetCurrentFortnightRange(today);

        List<DailyTimeEntry> currentFortnightEntries = await _dbContext.DailyTimeEntries
            .Where(x => x.UserId == userId && x.WorkDate >= startDate && x.WorkDate <= endDate)
            .OrderBy(x => x.WorkDate)
            .ToListAsync(cancellationToken);

        List<TilLedgerEntry> allLedgerRowsUpToEndDate = await _dbContext.TilLedgerEntries
            .Where(x => x.UserId == userId && x.WorkDate <= endDate)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.WorkDate)
            .ThenBy(x => x.CreatedUtc)
            .ToListAsync(cancellationToken);

        string templateFullPath = GetAbsolutePath(_options.TemplatePath);
        if (!File.Exists(templateFullPath))
        {
            throw new FileNotFoundException(
                $"Excel template was not found at '{templateFullPath}'. " +
                "Copy your master workbook to that location before exporting.");
        }

        string userExportFolder = Path.Combine(GetAbsolutePath(_options.ExportRootFolder), userId.ToString("N"));
        Directory.CreateDirectory(userExportFolder);

        DateTime utcNow = DateTime.UtcNow;
        string timestamp = utcNow.ToLocalTime().ToString("yyyy_MM_dd_HH_mm_ss");
        string fileName = $"{endDate:yyyy_MM_dd}_{timestamp}.xlsx";
        string outputFullPath = Path.Combine(userExportFolder, fileName);

        FortnightExport? existingExport = await _dbContext.FortnightExports
            .SingleOrDefaultAsync(
                x => x.UserId == userId &&
                     x.PeriodStartDate == startDate &&
                     x.PeriodEndDate == endDate,
                cancellationToken);

        if (existingExport is not null &&
            !string.IsNullOrWhiteSpace(existingExport.FilePath) &&
            File.Exists(existingExport.FilePath))
        {
            File.Delete(existingExport.FilePath);
        }

        File.Copy(templateFullPath, outputFullPath, overwrite: true);

        using (XLWorkbook workbook = new(outputFullPath))
        {
            IXLWorksheet timeSheet = workbook.Worksheet(_options.TimeSheetSheetName);
            IXLWorksheet tilBalanceSheet = workbook.Worksheet(_options.TilBalanceSheetName);

            ClearWritableEntryCells(timeSheet);
            FillTimeSheet(timeSheet, startDate, currentFortnightEntries);
            ValidateTimeSheetFormulas(timeSheet);

            ClearTilBalanceLedgerArea(tilBalanceSheet);
            FillTilBalanceLedger(tilBalanceSheet, allLedgerRowsUpToEndDate);

            workbook.Save();
        }

        if (existingExport is null)
        {
            existingExport = new FortnightExport
            {
                UserId = userId,
                PeriodStartDate = startDate,
                PeriodEndDate = endDate,
                FilePath = outputFullPath,
                FileName = fileName,
                CreatedUtc = utcNow
            };

            _dbContext.FortnightExports.Add(existingExport);
        }
        else
        {
            existingExport.FilePath = outputFullPath;
            existingExport.FileName = fileName;
            existingExport.CreatedUtc = utcNow;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return existingExport;
    }

    public async Task<IReadOnlyList<FortnightExport>> GetExportsAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await _dbContext.FortnightExports
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<FortnightExport?> GetExportByIdAsync(Guid userId, Guid exportId, CancellationToken cancellationToken)
    {
        return await _dbContext.FortnightExports
            .SingleOrDefaultAsync(x => x.UserId == userId && x.Id == exportId, cancellationToken);
    }

    private void FillTimeSheet(IXLWorksheet worksheet, DateOnly startDate, List<DailyTimeEntry> entries)
    {
        Dictionary<DateOnly, DailyTimeEntry> entryLookup = entries.ToDictionary(x => x.WorkDate);

        for (int dayIndex = 0; dayIndex < _options.TimeSheetDayCount; dayIndex++)
        {
            DateOnly workDate = startDate.AddDays(dayIndex);
            int row = _options.TimeSheetStartRow + dayIndex;

            entryLookup.TryGetValue(workDate, out DailyTimeEntry? entry);

            WriteDateCell(worksheet.Cell(row, "A"), workDate);
            worksheet.Cell(row, "B").Value = workDate.DayOfWeek.ToString();

            if (entry is null)
            {
                continue;
            }

            WriteClockTimeCell(worksheet.Cell(row, "C"), entry.StartTime);
            WriteClockTimeCell(worksheet.Cell(row, "D"), entry.FinishTime);
            WriteBreakMinutesCell(worksheet.Cell(row, "E"), entry.MealBreakMinutes);

            WriteHourDurationCell(worksheet.Cell(row, "F"), entry.PublicHolidayHours);
            WriteHourDurationCell(worksheet.Cell(row, "G"), entry.TimeInLieuAccruedHours);

            WriteHourDurationCell(worksheet.Cell(row, "I"), entry.AnnualLeaveHours);
            WriteHourDurationCell(worksheet.Cell(row, "J"), entry.SickLeaveHours);
            WriteHourDurationCell(worksheet.Cell(row, "K"), entry.LongServiceLeaveHours);
            WriteHourDurationCell(worksheet.Cell(row, "L"), entry.TimeInLieuTakenHours);

            WriteWorkedDayCommentCell(worksheet.Cell(row, "O"), entry);
        }
    }

    private void ClearTilBalanceLedgerArea(IXLWorksheet worksheet)
    {
        int startRow = _options.TilBalanceStartRow;
        int lastUsedRow = worksheet.LastRowUsed()?.RowNumber() ?? startRow;

        for (int row = startRow; row <= lastUsedRow; row++)
        {
            worksheet.Cell(row, "A").Clear(XLClearOptions.Contents);
            worksheet.Cell(row, "B").Clear(XLClearOptions.Contents);
            worksheet.Cell(row, "C").Clear(XLClearOptions.Contents);
            worksheet.Cell(row, "D").Clear(XLClearOptions.Contents);
            worksheet.Cell(row, "E").Clear(XLClearOptions.Contents);
        }
    }

    private void FillTilBalanceLedger(IXLWorksheet worksheet, List<TilLedgerEntry> ledgerRows)
    {
        int row = _options.TilBalanceStartRow;

        foreach (TilLedgerEntry ledgerRow in ledgerRows)
        {
            WriteDateCell(worksheet.Cell(row, "A"), ledgerRow.WorkDate);
            worksheet.Cell(row, "B").Value = string.IsNullOrWhiteSpace(ledgerRow.Description)
                ? GetDefaultLedgerDescription(ledgerRow)
                : ledgerRow.Description!.Trim();

            WriteDecimalHoursCell(worksheet.Cell(row, "C"), ledgerRow.HoursAccrued);
            WriteDecimalHoursCell(worksheet.Cell(row, "D"), ledgerRow.HoursTaken);

            if (row == _options.TilBalanceStartRow)
            {
                worksheet.Cell(row, "E").FormulaA1 = $"C{row}-D{row}";
            }
            else
            {
                worksheet.Cell(row, "E").FormulaA1 = $"E{row - 1}+C{row}-D{row}";
            }

            worksheet.Cell(row, "E").Style.NumberFormat.Format = "0.00";
            row++;
        }
    }

    private static string GetDefaultLedgerDescription(TilLedgerEntry ledgerRow)
    {
        if (ledgerRow.HoursAccrued > 0 && ledgerRow.HoursTaken == 0)
        {
            return "TIL Accrued";
        }

        if (ledgerRow.HoursTaken > 0 && ledgerRow.HoursAccrued == 0)
        {
            return "TIL Taken";
        }

        return "TIL Entry";
    }

    private void ValidateTimeSheetFormulas(IXLWorksheet worksheet)
    {
        int startRow = _options.TimeSheetStartRow;
        int endRow = _options.TimeSheetStartRow + _options.TimeSheetDayCount - 1;

        List<string> missingFormulaCells = new();

        for (int row = startRow; row <= endRow; row++)
        {
            string dayName = worksheet.Cell(row, "B").GetString().Trim();

            bool isWeekend =
                string.Equals(dayName, "Saturday", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(dayName, "Sunday", StringComparison.OrdinalIgnoreCase);

            if (isWeekend)
            {
                continue;
            }

            if (!worksheet.Cell(row, "H").HasFormula)
            {
                missingFormulaCells.Add($"H{row}");
            }

            if (!worksheet.Cell(row, "M").HasFormula)
            {
                missingFormulaCells.Add($"M{row}");
            }
        }

        if (missingFormulaCells.Count > 0)
        {
            throw new InvalidOperationException(
                $"Export validation failed. Missing formulas in: {string.Join(", ", missingFormulaCells)}");
        }
    }

    private void ClearWritableEntryCells(IXLWorksheet worksheet)
    {
        for (int row = _options.TimeSheetStartRow; row < _options.TimeSheetStartRow + _options.TimeSheetDayCount; row++)
        {
            worksheet.Cell(row, "A").Clear(XLClearOptions.Contents);
            worksheet.Cell(row, "B").Clear(XLClearOptions.Contents);
            worksheet.Cell(row, "C").Clear(XLClearOptions.Contents);
            worksheet.Cell(row, "D").Clear(XLClearOptions.Contents);
            worksheet.Cell(row, "E").Clear(XLClearOptions.Contents);
            worksheet.Cell(row, "F").Clear(XLClearOptions.Contents);
            worksheet.Cell(row, "G").Clear(XLClearOptions.Contents);
            worksheet.Cell(row, "I").Clear(XLClearOptions.Contents);
            worksheet.Cell(row, "J").Clear(XLClearOptions.Contents);
            worksheet.Cell(row, "K").Clear(XLClearOptions.Contents);
            worksheet.Cell(row, "L").Clear(XLClearOptions.Contents);
            worksheet.Cell(row, "O").Clear(XLClearOptions.Contents);
        }
    }

    private static void WriteDateCell(IXLCell cell, DateOnly value)
    {
        cell.Value = value.ToDateTime(TimeOnly.MinValue);
        cell.Style.DateFormat.Format = "d-MMM-yy";
    }

    private static void WriteClockTimeCell(IXLCell cell, TimeOnly? value)
    {
        if (!value.HasValue)
        {
            cell.Clear(XLClearOptions.Contents);
            return;
        }

        cell.Value = value.Value.ToTimeSpan();
        cell.Style.DateFormat.Format = "h:mm:ss";
    }

    private static void WriteBreakMinutesCell(IXLCell cell, int? mealBreakMinutes)
    {
        if (!mealBreakMinutes.HasValue || mealBreakMinutes.Value <= 0)
        {
            cell.Clear(XLClearOptions.Contents);
            return;
        }

        cell.Value = TimeSpan.FromMinutes(mealBreakMinutes.Value);
        cell.Style.DateFormat.Format = "h:mm:ss";
    }

    private static void WriteHourDurationCell(IXLCell cell, decimal hours)
    {
        if (hours == 0)
        {
            cell.Clear(XLClearOptions.Contents);
            return;
        }

        cell.Value = TimeSpan.FromHours((double)hours);
        cell.Style.DateFormat.Format = "h:mm:ss";
    }

    private static void WriteDecimalHoursCell(IXLCell cell, decimal hours)
    {
        if (hours == 0m)
        {
            cell.Clear(XLClearOptions.Contents);
            return;
        }

        cell.Value = hours;
        cell.Style.NumberFormat.Format = "0.00";
    }

    private static void WriteWorkedDayCommentCell(IXLCell cell, DailyTimeEntry entry)
    {
        bool shouldWriteComment =
            (entry.StartTime.HasValue && entry.FinishTime.HasValue) ||
            entry.SickLeaveHours > 0 ||
            entry.AnnualLeaveHours > 0 ||
            entry.LongServiceLeaveHours > 0;

        if (!shouldWriteComment || string.IsNullOrWhiteSpace(entry.Notes))
        {
            cell.Clear(XLClearOptions.Contents);
            return;
        }

        cell.Value = entry.Notes.Trim();
    }

    private string GetAbsolutePath(string relativeOrAbsolutePath)
    {
        if (Path.IsPathRooted(relativeOrAbsolutePath))
        {
            return relativeOrAbsolutePath;
        }

        return Path.Combine(_environment.ContentRootPath, relativeOrAbsolutePath);
    }
}