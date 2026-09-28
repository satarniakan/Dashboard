using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dashboard.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddChequeStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ChequeStatus",
                table: "SupplierPayments",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ChequeStatus",
                table: "CustomerReceipts",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ChequeStatus",
                table: "SupplierPayments");

            migrationBuilder.DropColumn(
                name: "ChequeStatus",
                table: "CustomerReceipts");
        }
    }
}
