using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dashboard.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSalesInvoiceToSalesRetur1n : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SalesInvoiceId",
                table: "SalesReturns",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalesReturns_SalesInvoiceId",
                table: "SalesReturns",
                column: "SalesInvoiceId");

            migrationBuilder.AddForeignKey(
                name: "FK_SalesReturns_SalesInvoices_SalesInvoiceId",
                table: "SalesReturns",
                column: "SalesInvoiceId",
                principalTable: "SalesInvoices",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SalesReturns_SalesInvoices_SalesInvoiceId",
                table: "SalesReturns");

            migrationBuilder.DropIndex(
                name: "IX_SalesReturns_SalesInvoiceId",
                table: "SalesReturns");

            migrationBuilder.DropColumn(
                name: "SalesInvoiceId",
                table: "SalesReturns");
        }
    }
}
