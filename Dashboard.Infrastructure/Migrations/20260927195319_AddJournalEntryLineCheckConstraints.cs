using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dashboard.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddJournalEntryLineCheckConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "CK_JournalEntryLines_NonNegative",
                table: "JournalEntryLines",
                sql: "[DebitAmount] >= 0 AND [CreditAmount] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_JournalEntryLines_OneSide",
                table: "JournalEntryLines",
                sql: "NOT ([DebitAmount] > 0 AND [CreditAmount] > 0)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_JournalEntryLines_NonNegative",
                table: "JournalEntryLines");

            migrationBuilder.DropCheckConstraint(
                name: "CK_JournalEntryLines_OneSide",
                table: "JournalEntryLines");
        }
    }
}
