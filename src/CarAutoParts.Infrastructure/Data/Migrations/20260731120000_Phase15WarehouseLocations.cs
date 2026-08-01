using CarAutoParts.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarAutoParts.Infrastructure.Data.Migrations;

/// <summary>Phase 15 — bin/location master, location balance dimension, putaway/pick columns.</summary>
[DbContext(typeof(ApplicationDbContext))]
[Migration("20260731120000_Phase15WarehouseLocations")]
public partial class Phase15WarehouseLocations : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "WarehouseLocations",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                WarehouseId = table.Column<int>(type: "int", nullable: false),
                Code = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                IsReceivingDefault = table.Column<bool>(type: "bit", nullable: false),
                IsPickDefault = table.Column<bool>(type: "bit", nullable: false),
                IsActive = table.Column<bool>(type: "bit", nullable: false),
                SortOrder = table.Column<int>(type: "int", nullable: false),
                CompanyId = table.Column<int>(type: "int", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_WarehouseLocations", x => x.Id);
                table.ForeignKey(
                    name: "FK_WarehouseLocations_Warehouses_WarehouseId",
                    column: x => x.WarehouseId,
                    principalTable: "Warehouses",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_WarehouseLocations_WarehouseId_Code",
            table: "WarehouseLocations",
            columns: new[] { "WarehouseId", "Code" },
            unique: true);

        migrationBuilder.CreateTable(
            name: "InventoryLocationBalances",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                InventoryItemId = table.Column<int>(type: "int", nullable: false),
                WarehouseLocationId = table.Column<int>(type: "int", nullable: false),
                QuantityOnHand = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_InventoryLocationBalances", x => x.Id);
                table.ForeignKey(
                    name: "FK_InventoryLocationBalances_InventoryItems_InventoryItemId",
                    column: x => x.InventoryItemId,
                    principalTable: "InventoryItems",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_InventoryLocationBalances_WarehouseLocations_WarehouseLocationId",
                    column: x => x.WarehouseLocationId,
                    principalTable: "WarehouseLocations",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_InventoryLocationBalances_InventoryItemId_WarehouseLocationId",
            table: "InventoryLocationBalances",
            columns: new[] { "InventoryItemId", "WarehouseLocationId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_InventoryLocationBalances_WarehouseLocationId",
            table: "InventoryLocationBalances",
            column: "WarehouseLocationId");

        migrationBuilder.AddColumn<int>(
            name: "WarehouseLocationId",
            table: "GoodsReceiptLines",
            type: "int",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_GoodsReceiptLines_WarehouseLocationId",
            table: "GoodsReceiptLines",
            column: "WarehouseLocationId");

        migrationBuilder.AddForeignKey(
            name: "FK_GoodsReceiptLines_WarehouseLocations_WarehouseLocationId",
            table: "GoodsReceiptLines",
            column: "WarehouseLocationId",
            principalTable: "WarehouseLocations",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddColumn<int>(
            name: "WarehouseLocationId",
            table: "CycleCounts",
            type: "int",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_CycleCounts_WarehouseLocationId",
            table: "CycleCounts",
            column: "WarehouseLocationId");

        migrationBuilder.AddForeignKey(
            name: "FK_CycleCounts_WarehouseLocations_WarehouseLocationId",
            table: "CycleCounts",
            column: "WarehouseLocationId",
            principalTable: "WarehouseLocations",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddColumn<int>(
            name: "WarehouseLocationId",
            table: "CycleCountLines",
            type: "int",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_CycleCountLines_WarehouseLocationId",
            table: "CycleCountLines",
            column: "WarehouseLocationId");

        migrationBuilder.AddForeignKey(
            name: "FK_CycleCountLines_WarehouseLocations_WarehouseLocationId",
            table: "CycleCountLines",
            column: "WarehouseLocationId",
            principalTable: "WarehouseLocations",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddColumn<int>(
            name: "FromLocationId",
            table: "InventoryTransferLines",
            type: "int",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "ToLocationId",
            table: "InventoryTransferLines",
            type: "int",
            nullable: true);

        migrationBuilder.AddColumn<bool>(
            name: "IsPicked",
            table: "InventoryTransferLines",
            type: "bit",
            nullable: false,
            defaultValue: false);

        migrationBuilder.CreateIndex(
            name: "IX_InventoryTransferLines_FromLocationId",
            table: "InventoryTransferLines",
            column: "FromLocationId");

        migrationBuilder.CreateIndex(
            name: "IX_InventoryTransferLines_ToLocationId",
            table: "InventoryTransferLines",
            column: "ToLocationId");

        migrationBuilder.AddForeignKey(
            name: "FK_InventoryTransferLines_WarehouseLocations_FromLocationId",
            table: "InventoryTransferLines",
            column: "FromLocationId",
            principalTable: "WarehouseLocations",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "FK_InventoryTransferLines_WarehouseLocations_ToLocationId",
            table: "InventoryTransferLines",
            column: "ToLocationId",
            principalTable: "WarehouseLocations",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddColumn<int>(
            name: "FromLocationId",
            table: "DeliveryNoteLines",
            type: "int",
            nullable: true);

        migrationBuilder.AddColumn<bool>(
            name: "IsPicked",
            table: "DeliveryNoteLines",
            type: "bit",
            nullable: false,
            defaultValue: false);

        migrationBuilder.CreateIndex(
            name: "IX_DeliveryNoteLines_FromLocationId",
            table: "DeliveryNoteLines",
            column: "FromLocationId");

        migrationBuilder.AddForeignKey(
            name: "FK_DeliveryNoteLines_WarehouseLocations_FromLocationId",
            table: "DeliveryNoteLines",
            column: "FromLocationId",
            principalTable: "WarehouseLocations",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);

        // Seed MAIN bin per warehouse and backfill location balances from warehouse rollup.
        migrationBuilder.Sql("""
            INSERT INTO WarehouseLocations (WarehouseId, Code, Name, IsReceivingDefault, IsPickDefault, IsActive, SortOrder, CompanyId, CreatedAt, CreatedBy, IsDeleted)
            SELECT w.Id, N'MAIN', N'Main', 1, 1, 1, 0, w.CompanyId, SYSUTCDATETIME(), N'migration', 0
            FROM Warehouses w
            WHERE w.IsDeleted = 0
              AND NOT EXISTS (
                  SELECT 1 FROM WarehouseLocations l
                  WHERE l.WarehouseId = w.Id AND l.Code = N'MAIN' AND l.IsDeleted = 0);
            """);

        migrationBuilder.Sql("""
            INSERT INTO InventoryLocationBalances (InventoryItemId, WarehouseLocationId, QuantityOnHand, CreatedAt, CreatedBy, IsDeleted)
            SELECT i.Id, l.Id, i.QuantityOnHand, SYSUTCDATETIME(), N'migration', 0
            FROM InventoryItems i
            INNER JOIN WarehouseLocations l ON l.WarehouseId = i.WarehouseId AND l.Code = N'MAIN' AND l.IsDeleted = 0
            WHERE i.IsDeleted = 0
              AND i.QuantityOnHand <> 0
              AND NOT EXISTS (
                  SELECT 1 FROM InventoryLocationBalances b
                  WHERE b.InventoryItemId = i.Id AND b.WarehouseLocationId = l.Id AND b.IsDeleted = 0);
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_DeliveryNoteLines_WarehouseLocations_FromLocationId",
            table: "DeliveryNoteLines");
        migrationBuilder.DropIndex(name: "IX_DeliveryNoteLines_FromLocationId", table: "DeliveryNoteLines");
        migrationBuilder.DropColumn(name: "FromLocationId", table: "DeliveryNoteLines");
        migrationBuilder.DropColumn(name: "IsPicked", table: "DeliveryNoteLines");

        migrationBuilder.DropForeignKey(
            name: "FK_InventoryTransferLines_WarehouseLocations_FromLocationId",
            table: "InventoryTransferLines");
        migrationBuilder.DropForeignKey(
            name: "FK_InventoryTransferLines_WarehouseLocations_ToLocationId",
            table: "InventoryTransferLines");
        migrationBuilder.DropIndex(name: "IX_InventoryTransferLines_FromLocationId", table: "InventoryTransferLines");
        migrationBuilder.DropIndex(name: "IX_InventoryTransferLines_ToLocationId", table: "InventoryTransferLines");
        migrationBuilder.DropColumn(name: "FromLocationId", table: "InventoryTransferLines");
        migrationBuilder.DropColumn(name: "ToLocationId", table: "InventoryTransferLines");
        migrationBuilder.DropColumn(name: "IsPicked", table: "InventoryTransferLines");

        migrationBuilder.DropForeignKey(
            name: "FK_CycleCountLines_WarehouseLocations_WarehouseLocationId",
            table: "CycleCountLines");
        migrationBuilder.DropIndex(name: "IX_CycleCountLines_WarehouseLocationId", table: "CycleCountLines");
        migrationBuilder.DropColumn(name: "WarehouseLocationId", table: "CycleCountLines");

        migrationBuilder.DropForeignKey(
            name: "FK_CycleCounts_WarehouseLocations_WarehouseLocationId",
            table: "CycleCounts");
        migrationBuilder.DropIndex(name: "IX_CycleCounts_WarehouseLocationId", table: "CycleCounts");
        migrationBuilder.DropColumn(name: "WarehouseLocationId", table: "CycleCounts");

        migrationBuilder.DropForeignKey(
            name: "FK_GoodsReceiptLines_WarehouseLocations_WarehouseLocationId",
            table: "GoodsReceiptLines");
        migrationBuilder.DropIndex(name: "IX_GoodsReceiptLines_WarehouseLocationId", table: "GoodsReceiptLines");
        migrationBuilder.DropColumn(name: "WarehouseLocationId", table: "GoodsReceiptLines");

        migrationBuilder.DropTable(name: "InventoryLocationBalances");
        migrationBuilder.DropTable(name: "WarehouseLocations");
    }
}
