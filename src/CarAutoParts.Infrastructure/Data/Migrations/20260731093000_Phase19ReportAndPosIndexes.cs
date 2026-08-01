using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarAutoParts.Infrastructure.Data.Migrations;

/// <summary>Phase 19 — indexes for day-range reports and fitment filters.</summary>
public partial class Phase19ReportAndPosIndexes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateIndex(
            name: "IX_SalesInvoices_InvoiceDate_WarehouseId",
            table: "SalesInvoices",
            columns: new[] { "InvoiceDate", "WarehouseId" });

        migrationBuilder.CreateIndex(
            name: "IX_SalesReturns_ReturnDate",
            table: "SalesReturns",
            column: "ReturnDate");

        migrationBuilder.CreateIndex(
            name: "IX_ProductVehicleCompatibilities_Make_Model",
            table: "ProductVehicleCompatibilities",
            columns: new[] { "Make", "Model" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_SalesInvoices_InvoiceDate_WarehouseId",
            table: "SalesInvoices");

        migrationBuilder.DropIndex(
            name: "IX_SalesReturns_ReturnDate",
            table: "SalesReturns");

        migrationBuilder.DropIndex(
            name: "IX_ProductVehicleCompatibilities_Make_Model",
            table: "ProductVehicleCompatibilities");
    }
}
