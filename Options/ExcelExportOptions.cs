namespace TimesheetAutomation.Web.Options;

public sealed class ExcelExportOptions
{
    public string TemplatePath { get; set; } = "Templates/Master_Timesheet_Template.xlsx";

    public string ExportRootFolder { get; set; } = "Exports";

    public string TimeSheetSheetName { get; set; } = "Time sheet";

    public string TilBalanceSheetName { get; set; } = "TIL Balance";

    public int TimeSheetStartRow { get; set; } = 7;

    public int TimeSheetDayCount { get; set; } = 14;

    public int TilBalanceHeaderRows { get; set; } = 5;

    public int TilBalanceStartRow { get; set; } = 6;
}