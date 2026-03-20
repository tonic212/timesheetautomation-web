using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TimesheetAutomation.Web.Migrations.App
{
    /// <inheritdoc />
    public partial class InitialAppDb : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DailyTimeEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    WorkDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "TEXT", nullable: true),
                    FinishTime = table.Column<TimeOnly>(type: "TEXT", nullable: true),
                    MealBreakMinutes = table.Column<int>(type: "INTEGER", nullable: true),
                    IsPublicHoliday = table.Column<bool>(type: "INTEGER", nullable: false),
                    PublicHolidayHours = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    TimeInLieuAccruedHours = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    AnnualLeaveHours = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    SickLeaveHours = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    LongServiceLeaveHours = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    TimeInLieuTakenHours = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    IsLocked = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastModifiedUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyTimeEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FortnightExports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PeriodStartDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    PeriodEndDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    FilePath = table.Column<string>(type: "TEXT", maxLength: 260, nullable: false),
                    FileName = table.Column<string>(type: "TEXT", maxLength: 260, nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FortnightExports", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TimeEntryAudits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DailyTimeEntryId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    FieldName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    OldValue = table.Column<string>(type: "TEXT", nullable: true),
                    NewValue = table.Column<string>(type: "TEXT", nullable: true),
                    ChangedUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TimeEntryAudits", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TilLedgerEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SourceDailyTimeEntryId = table.Column<Guid>(type: "TEXT", nullable: true),
                    SourceKind = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    WorkDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    EntryType = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Hours = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastModifiedUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TilLedgerEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TilLedgerEntries_DailyTimeEntries_SourceDailyTimeEntryId",
                        column: x => x.SourceDailyTimeEntryId,
                        principalTable: "DailyTimeEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DailyTimeEntries_UserId_WorkDate",
                table: "DailyTimeEntries",
                columns: new[] { "UserId", "WorkDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FortnightExports_UserId",
                table: "FortnightExports",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_TilLedgerEntries_SourceDailyTimeEntryId",
                table: "TilLedgerEntries",
                column: "SourceDailyTimeEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_TilLedgerEntries_UserId",
                table: "TilLedgerEntries",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_TilLedgerEntries_UserId_WorkDate",
                table: "TilLedgerEntries",
                columns: new[] { "UserId", "WorkDate" });

            migrationBuilder.CreateIndex(
                name: "IX_TimeEntryAudits_DailyTimeEntryId_ChangedUtc",
                table: "TimeEntryAudits",
                columns: new[] { "DailyTimeEntryId", "ChangedUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FortnightExports");

            migrationBuilder.DropTable(
                name: "TilLedgerEntries");

            migrationBuilder.DropTable(
                name: "TimeEntryAudits");

            migrationBuilder.DropTable(
                name: "DailyTimeEntries");
        }
    }
}
