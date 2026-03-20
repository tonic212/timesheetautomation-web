using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TimesheetAutomation.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddTilLedgerEntries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TilLedgerEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SourceDailyTimeEntryId = table.Column<Guid>(type: "TEXT", nullable: false),
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
                    table.ForeignKey(
                        name: "FK_TilLedgerEntries_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TilLedgerEntries_SourceDailyTimeEntryId_EntryType",
                table: "TilLedgerEntries",
                columns: new[] { "SourceDailyTimeEntryId", "EntryType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TilLedgerEntries_UserId",
                table: "TilLedgerEntries",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TilLedgerEntries");
        }
    }
}
