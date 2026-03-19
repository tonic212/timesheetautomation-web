using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TimesheetAutomation.Web.Migrations
{
    /// <inheritdoc />
    public partial class ChangeMealBreakToMinutesAndUpdateEntryUi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MealBreak",
                table: "DailyTimeEntries");

            migrationBuilder.AddColumn<int>(
                name: "MealBreakMinutes",
                table: "DailyTimeEntries",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MealBreakMinutes",
                table: "DailyTimeEntries");

            migrationBuilder.AddColumn<TimeOnly>(
                name: "MealBreak",
                table: "DailyTimeEntries",
                type: "TEXT",
                nullable: true);
        }
    }
}
