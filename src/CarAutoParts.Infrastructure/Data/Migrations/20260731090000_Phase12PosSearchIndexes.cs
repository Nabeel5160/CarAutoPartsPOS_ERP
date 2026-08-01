using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarAutoParts.Infrastructure.Data.Migrations;

/// <summary>Phase 12 — OEM/part indexes for POS exact-match search hot path.</summary>
public partial class Phase12PosSearchIndexes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateIndex(
            name: "IX_Products_CompanyId_OemNumber",
            table: "Products",
            columns: new[] { "CompanyId", "OemNumber" },
            filter: "[OemNumber] IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_Products_CompanyId_PartNumber",
            table: "Products",
            columns: new[] { "CompanyId", "PartNumber" },
            filter: "[PartNumber] IS NOT NULL");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_Products_CompanyId_OemNumber",
            table: "Products");

        migrationBuilder.DropIndex(
            name: "IX_Products_CompanyId_PartNumber",
            table: "Products");
    }
}
