using CarAutoParts.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarAutoParts.Infrastructure.Data.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260728120000_Phase5MultiBranch")]
    public partial class Phase5MultiBranch : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ShippedUnitCost",
                table: "InventoryTransferLines",
                type: "decimal(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "DeclaredClosingCash",
                table: "CashierShifts",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ExpectedCash",
                table: "CashierShifts",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "CashVariance",
                table: "CashierShifts",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "VarianceJournalEntryId",
                table: "CashierShifts",
                type: "int",
                nullable: true);

            // Backfill null warehouse BranchId from first company branch (SQL Server).
            // Use correlated subquery — avoid CTE/; which can be mangled by SQL batching.
            migrationBuilder.Sql(@"
UPDATE Warehouses
SET BranchId = (
    SELECT MIN(b.Id)
    FROM Branches b
    WHERE b.CompanyId = Warehouses.CompanyId AND b.IsDeleted = 0
)
WHERE BranchId IS NULL AND IsDeleted = 0
  AND EXISTS (
      SELECT 1 FROM Branches b
      WHERE b.CompanyId = Warehouses.CompanyId AND b.IsDeleted = 0
  )
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "ShippedUnitCost", table: "InventoryTransferLines");
            migrationBuilder.DropColumn(name: "DeclaredClosingCash", table: "CashierShifts");
            migrationBuilder.DropColumn(name: "ExpectedCash", table: "CashierShifts");
            migrationBuilder.DropColumn(name: "CashVariance", table: "CashierShifts");
            migrationBuilder.DropColumn(name: "VarianceJournalEntryId", table: "CashierShifts");
        }
    }
}
