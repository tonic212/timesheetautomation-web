using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TimesheetAutomation.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddImportedTilLedgerSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TilLedgerEntries_SourceDailyTimeEntryId_EntryType",
                table: "TilLedgerEntries");

            migrationBuilder.AlterColumn<Guid>(
                name: "SourceDailyTimeEntryId",
                table: "TilLedgerEntries",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "TEXT");

            migrationBuilder.AddColumn<string>(
                name: "SourceKind",
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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TilLedgerEntries_SourceDailyTimeEntryId",
                table: "TilLedgerEntries");

            migrationBuilder.DropIndex(
                name: "IX_TilLedgerEntries_UserId_WorkDate",
                table: "TilLedgerEntries");

            migrationBuilder.DropColumn(
                name: "SourceKind",
                table: "TilLedgerEntries");

            migrationBuilder.AlterColumn<Guid>(
                name: "SourceDailyTimeEntryId",
                table: "TilLedgerEntries",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TilLedgerEntries_SourceDailyTimeEntryId_EntryType",
                table: "TilLedgerEntries",
                columns: new[] { "SourceDailyTimeEntryId", "EntryType" },
                unique: true);
        }
    }
}
