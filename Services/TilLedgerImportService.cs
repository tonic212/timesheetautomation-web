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

        int nextSortOrder = await _dbContext.TilLedgerEntries
            .Where(x => x.UserId == userId)
            .Select(x => (int?)x.SortOrder)
            .MaxAsync(cancellationToken) ?? 0;

        int importedCount = 0;

        // Row 5 is opening balance/header.
        // Row 6 is the first expected ledger row.
        int startRow = 6;
        int lastUsedRow = worksheet.LastRowUsed()?.RowNumber() ?? startRow;

        for (int row = startRow; row <= lastUsedRow; row++)
        {
            IXLCell dateCell = worksheet.Cell(row, "A");
            IXLCell reasonCell = worksheet.Cell(row, "B");
            IXLCell accruedCell = worksheet.Cell(row, "C");
            IXLCell takenCell = worksheet.Cell(row, "D");
            IXLCell balanceCell = worksheet.Cell(row, "E");

            bool rowIsCompletelyEmpty =
                IsEffectivelyEmpty(dateCell) &&
                IsEffectivelyEmpty(reasonCell) &&
                IsEffectivelyEmpty(accruedCell) &&
                IsEffectivelyEmpty(takenCell) &&
                IsEffectivelyEmpty(balanceCell);

            if (rowIsCompletelyEmpty)
            {
                continue;
            }

            if (!TryReadDate(dateCell, out DateOnly workDate))
            {
                continue;
            }

            decimal hoursAccrued = ReadDecimalHours(accruedCell);
            decimal hoursTaken = ReadDecimalHours(takenCell);
            string? description = Normalize(reasonCell.GetFormattedString());

            if (hoursAccrued == 0m && hoursTaken == 0m)
            {
                continue;
            }

            nextSortOrder++;

            TilLedgerEntry imported = new()
            {
                UserId = userId,
                SourceDailyTimeEntryId = null,
                SourceKind = "Imported",
                WorkDate = workDate,
                Description = description,
                HoursAccrued = hoursAccrued,
                HoursTaken = hoursTaken,
                SortOrder = nextSortOrder,
                CreatedUtc = DateTime.UtcNow,
                LastModifiedUtc = DateTime.UtcNow
            };

            _dbContext.TilLedgerEntries.Add(imported);
            importedCount++;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return importedCount;
    }

    private static bool TryReadDate(IXLCell cell, out DateOnly date)
    {
        if (cell.TryGetValue<DateTime>(out DateTime dateTime))
        {
            date = DateOnly.FromDateTime(dateTime);
            return true;
        }

        string formatted = cell.GetFormattedString().Trim();
        if (string.IsNullOrWhiteSpace(formatted))
        {
            date = default;
            return false;
        }

        if (DateOnly.TryParse(formatted, out DateOnly parsedDate))
        {
            date = parsedDate;
            return true;
        }

        if (DateTime.TryParse(formatted, out DateTime parsedDateTime))
        {
            date = DateOnly.FromDateTime(parsedDateTime);
            return true;
        }

        date = default;
        return false;
    }

    private static decimal ReadDecimalHours(IXLCell cell)
    {
        if (IsEffectivelyEmpty(cell))
        {
            return 0m;
        }

        if (cell.TryGetValue<decimal>(out decimal decimalValue))
        {
            return Math.Round(decimalValue, 2, MidpointRounding.AwayFromZero);
        }

        if (cell.TryGetValue<double>(out double doubleValue))
        {
            return Math.Round((decimal)doubleValue, 2, MidpointRounding.AwayFromZero);
        }

        string text = cell.GetFormattedString().Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            return 0m;
        }

        if (decimal.TryParse(text, out decimal parsed))
        {
            return Math.Round(parsed, 2, MidpointRounding.AwayFromZero);
        }

        return 0m;
    }

    private static bool IsEffectivelyEmpty(IXLCell cell)
    {
        if (cell.IsEmpty())
        {
            return true;
        }

        string text = cell.GetFormattedString().Trim();
        return string.IsNullOrWhiteSpace(text);
    }

    private static string? Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}