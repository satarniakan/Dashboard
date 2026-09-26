using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dashboard.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class OutboxEmailChannel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(
                name: "SmsOutbox",
                newName: "OutboxMessages");

            migrationBuilder.AddColumn<int>(
                name: "Channel",
                table: "OutboxMessages",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "Subject",
                table: "OutboxMessages",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);
            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_Status_Attempts",
                table: "OutboxMessages",
                columns: new[] { "Status", "Attempts" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Subject",
                table: "OutboxMessages");

            migrationBuilder.DropColumn(
                name: "Channel",
                table: "OutboxMessages");

            migrationBuilder.RenameTable(
                name: "OutboxMessages",
                newName: "SmsOutbox");
        }
    }
}
