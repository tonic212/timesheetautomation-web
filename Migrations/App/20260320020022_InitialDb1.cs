using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TimesheetAutomation.Web.Migrations.App
{
    /// <inheritdoc />
    public partial class InitialDb1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TilLedgerEntries_SourceDailyTimeEntryId",
                table: "TilLedgerEntries");

            migrationBuilder.DropIndex(
                name: "IX_TilLedgerEntries_UserId_WorkDate",
                table: "TilLedgerEntries");

            migrationBuilder.DropColumn(
                name: "EntryType",
                table: "TilLedgerEntries");

            migrationBuilder.RenameColumn(
                name: "Hours",
                table: "TilLedgerEntries",
                newName: "HoursTaken");

            migrationBuilder.AddColumn<decimal>(
                name: "HoursAccrued",
                table: "TilLedgerEntries",
                type: "decimal(5,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "SortOrder",
                table: "TilLedgerEntries",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_TilLedgerEntries_SourceDailyTimeEntryId_SourceKind",
                table: "TilLedgerEntries",
                columns: new[] { "SourceDailyTimeEntryId", "SourceKind" });

            migrationBuilder.CreateIndex(
                name: "IX_TilLedgerEntries_UserId_SortOrder",
                table: "TilLedgerEntries",
                columns: new[] { "UserId", "SortOrder" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TilLedgerEntries_SourceDailyTimeEntryId_SourceKind",
                table: "TilLedgerEntries");

            migrationBuilder.DropIndex(
                name: "IX_TilLedgerEntries_UserId_SortOrder",
                table: "TilLedgerEntries");

            migrationBuilder.DropColumn(
                name: "HoursAccrued",
                table: "TilLedgerEntries");

            migrationBuilder.DropColumn(
                name: "SortOrder",
                table: "TilLedgerEntries");

            migrationBuilder.RenameColumn(
                name: "HoursTaken",
                table: "TilLedgerEntries",
                newName: "Hours");

            migrationBuilder.AddColumn<string>(
                name: "EntryType",
                table: "TilLedgerEntries",
                type: "TEXT",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_TilLedgerEntries_SourceDailyTimeEntryId",
                table: "TilLedgerEntries",
                column: "SourceDailyTimeEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_TilLedgerEntries_UserId_WorkDate",
                table: "TilLedgerEntries",
                columns: new[] { "UserId", "WorkDate" });
        }
    }
}
