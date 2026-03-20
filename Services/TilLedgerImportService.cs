using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using TimesheetAutomation.Web.Data;
using TimesheetAutomation.Web.Models;

namespace TimesheetAutomation.Web.Services;

public sealed class TilLedgerImportService : ITilLedgerImportService
{
    private readonly AppDbContext _dbContext;

    public TilLedgerImportService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<int> ImportLatestWorkbookAsync(Guid userId, Stream fileStream, CancellationToken cancellationToken)
    {
        using XLWorkbook workbook = new(fileStream);

        if (!workbook.TryGetWorksheet("TIL Balance", out IXLWorksheet? worksheet) || worksheet is null)
        {
            throw new InvalidOperationException("The uploaded workbook does not contain a 'TIL Balance' sheet.");
        }

        List<TilLedgerEntry> existingImportedRows = await _dbContext.TilLedgerEntries
            .Where(x => x.UserId == userId && x.SourceKind == "Imported")
            .ToListAsync(cancellationToken);

        if (existingImportedRows.Count > 0)
        {
            _dbContext.TilLedgerEntries.RemoveRange(existingImportedRows);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        List<TilLedgerEntry> existingDailyRows = await _dbContext.TilLedgerEntries
            .Where(x => x.UserId == userId && x.SourceKind == "DailyEntry")
            .ToListAsync(cancellationToken);

        int importedCount = 0;
        int row = 6;

        while (true)
        {
            IXLCell dateCell = worksheet.Cell(row, "A");
            IXLCell descriptionCell = worksheet.Cell(row, "B");
            IXLCell hoursCell = worksheet.Cell(row, "C");

            bool rowIsEmpty =
                dateCell.IsEmpty() &&
                descriptionCell.IsEmpty() &&
                hoursCell.IsEmpty();

            if (rowIsEmpty)
            {
                break;
            }

            if (dateCell.IsEmpty() || hoursCell.IsEmpty())
            {
                row++;
                continue;
            }

            DateOnly workDate = ReadDate(dateCell);
            decimal signedHours = ReadSignedHours(hoursCell);

            if (signedHours == 0)
            {
                row++;
                continue;
            }

            string entryType = signedHours < 0 ? "Taken" : "Accrued";
            decimal absoluteHours = Math.Abs(signedHours);
            string? description = Normalize(descriptionCell.GetString());

            bool alreadyExistsAsDailyEntry = existingDailyRows.Any(x =>
                x.WorkDate == workDate &&
                string.Equals(x.EntryType, entryType, StringComparison.OrdinalIgnoreCase) &&
                x.Hours == absoluteHours &&
                string.Equals(Normalize(x.Description), description, StringComparison.Ordinal));

            if (!alreadyExistsAsDailyEntry)
            {
                TilLedgerEntry imported = new()
                {
                    UserId = userId,
                    SourceDailyTimeEntryId = null,
                    SourceKind = "Imported",
                    WorkDate = workDate,
                    EntryType = entryType,
                    Hours = absoluteHours,
                    Description = description,
                    CreatedUtc = DateTime.UtcNow,
                    LastModifiedUtc = DateTime.UtcNow
                };

                _dbContext.TilLedgerEntries.Add(imported);
                importedCount++;
            }

            row++;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return importedCount;
    }

    private static DateOnly ReadDate(IXLCell cell)
    {
        if (cell.TryGetValue<DateTime>(out DateTime dateTime))
        {
            return DateOnly.FromDateTime(dateTime);
        }

        if (DateOnly.TryParse(cell.GetString(), out DateOnly dateOnly))
        {
            return dateOnly;
        }

        throw new InvalidOperationException($"Could not read a valid date from cell {cell.Address}.");
    }

    private static decimal ReadSignedHours(IXLCell cell)
    {
        if (cell.TryGetValue<TimeSpan>(out TimeSpan timeSpan))
        {
            return Math.Round((decimal)timeSpan.TotalHours, 2, MidpointRounding.AwayFromZero);
        }

        if (cell.TryGetValue<double>(out double numericValue))
        {
            return Math.Round((decimal)(numericValue * 24d), 2, MidpointRounding.AwayFromZero);
        }

        string text = cell.GetFormattedString().Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            return 0m;
        }

        if (TimeSpan.TryParse(text, out TimeSpan parsedTime))
        {
            return Math.Round((decimal)parsedTime.TotalHours, 2, MidpointRounding.AwayFromZero);
        }

        throw new InvalidOperationException($"Could not read valid hours from cell {cell.Address}.");
    }

    private static string? Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}