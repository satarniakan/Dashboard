using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dashboard.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddInstallments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SubsidiaryId",
                table: "JournalEntryLines",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SubsidiaryType",
                table: "JournalEntryLines",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "InstallmentId",
                table: "CustomerReceipts",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "InstallmentPlans",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SalesInvoiceId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InstallmentPlans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InstallmentPlans_SalesInvoices_SalesInvoiceId",
                        column: x => x.SalesInvoiceId,
                        principalTable: "SalesInvoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Installments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InstallmentPlanId = table.Column<int>(type: "int", nullable: false),
                    SequenceNumber = table.Column<int>(type: "int", nullable: false),
                    DueDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PaidAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Installments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Installments_InstallmentPlans_InstallmentPlanId",
                        column: x => x.InstallmentPlanId,
                        principalTable: "InstallmentPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntryLines_SubsidiaryType_SubsidiaryId",
                table: "JournalEntryLines",
                columns: new[] { "SubsidiaryType", "SubsidiaryId" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerReceipts_InstallmentId",
                table: "CustomerReceipts",
                column: "InstallmentId");

            migrationBuilder.CreateIndex(
                name: "IX_InstallmentPlans_SalesInvoiceId",
                table: "InstallmentPlans",
                column: "SalesInvoiceId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Installments_InstallmentPlanId",
                table: "Installments",
                column: "InstallmentPlanId");

            migrationBuilder.AddForeignKey(
                name: "FK_CustomerReceipts_Installments_InstallmentId",
                table: "CustomerReceipts",
                column: "InstallmentId",
                principalTable: "Installments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CustomerReceipts_Installments_InstallmentId",
                table: "CustomerReceipts");

            migrationBuilder.DropTable(
                name: "Installments");

            migrationBuilder.DropTable(
                name: "InstallmentPlans");

            migrationBuilder.DropIndex(
                name: "IX_JournalEntryLines_SubsidiaryType_SubsidiaryId",
                table: "JournalEntryLines");

            migrationBuilder.DropIndex(
                name: "IX_CustomerReceipts_InstallmentId",
                table: "CustomerReceipts");

            migrationBuilder.DropColumn(
                name: "SubsidiaryId",
                table: "JournalEntryLines");

            migrationBuilder.DropColumn(
                name: "SubsidiaryType",
                table: "JournalEntryLines");

            migrationBuilder.DropColumn(
                name: "InstallmentId",
                table: "CustomerReceipts");
        }
    }
}
