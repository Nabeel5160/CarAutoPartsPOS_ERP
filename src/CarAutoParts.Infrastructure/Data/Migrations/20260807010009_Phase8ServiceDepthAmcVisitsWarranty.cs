using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarAutoParts.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase8ServiceDepthAmcVisitsWarranty : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AmcContractId",
                table: "ServiceTickets",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReplacementProductId",
                table: "ServiceTickets",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ReplacementQuantity",
                table: "ServiceTickets",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "WarrantyEvidenceNotes",
                table: "ServiceTickets",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WarrantySalesInvoiceId",
                table: "ServiceTickets",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AmcContracts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ContractNumber = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    CustomerId = table.Column<int>(type: "int", nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CoverageNotes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    AnnualAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    ProductId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CompanyId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AmcContracts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AmcContracts_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AmcContracts_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ServiceTicketParts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ServiceTicketId = table.Column<int>(type: "int", nullable: false),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    WarehouseId = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    UnitCost = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    ConsumedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CompanyId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceTicketParts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ServiceTicketParts_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ServiceTicketParts_ServiceTickets_ServiceTicketId",
                        column: x => x.ServiceTicketId,
                        principalTable: "ServiceTickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ServiceTicketParts_Warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalTable: "Warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ServiceVisits",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ServiceTicketId = table.Column<int>(type: "int", nullable: false),
                    AssignedToUserId = table.Column<int>(type: "int", nullable: false),
                    ScheduledAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CompanyId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceVisits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ServiceVisits_ServiceTickets_ServiceTicketId",
                        column: x => x.ServiceTicketId,
                        principalTable: "ServiceTickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ServiceVisits_Users_AssignedToUserId",
                        column: x => x.AssignedToUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceTickets_AmcContractId",
                table: "ServiceTickets",
                column: "AmcContractId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceTickets_ReplacementProductId",
                table: "ServiceTickets",
                column: "ReplacementProductId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceTickets_WarrantySalesInvoiceId",
                table: "ServiceTickets",
                column: "WarrantySalesInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_AmcContracts_CompanyId_ContractNumber",
                table: "AmcContracts",
                columns: new[] { "CompanyId", "ContractNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AmcContracts_CompanyId_CustomerId_Status",
                table: "AmcContracts",
                columns: new[] { "CompanyId", "CustomerId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_AmcContracts_CustomerId",
                table: "AmcContracts",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_AmcContracts_ProductId",
                table: "AmcContracts",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceTicketParts_ProductId",
                table: "ServiceTicketParts",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceTicketParts_ServiceTicketId",
                table: "ServiceTicketParts",
                column: "ServiceTicketId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceTicketParts_WarehouseId",
                table: "ServiceTicketParts",
                column: "WarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceVisits_AssignedToUserId",
                table: "ServiceVisits",
                column: "AssignedToUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceVisits_CompanyId_AssignedToUserId_ScheduledAt",
                table: "ServiceVisits",
                columns: new[] { "CompanyId", "AssignedToUserId", "ScheduledAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceVisits_ServiceTicketId_Status",
                table: "ServiceVisits",
                columns: new[] { "ServiceTicketId", "Status" });

            migrationBuilder.AddForeignKey(
                name: "FK_ServiceTickets_AmcContracts_AmcContractId",
                table: "ServiceTickets",
                column: "AmcContractId",
                principalTable: "AmcContracts",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ServiceTickets_Products_ReplacementProductId",
                table: "ServiceTickets",
                column: "ReplacementProductId",
                principalTable: "Products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ServiceTickets_SalesInvoices_WarrantySalesInvoiceId",
                table: "ServiceTickets",
                column: "WarrantySalesInvoiceId",
                principalTable: "SalesInvoices",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ServiceTickets_AmcContracts_AmcContractId",
                table: "ServiceTickets");

            migrationBuilder.DropForeignKey(
                name: "FK_ServiceTickets_Products_ReplacementProductId",
                table: "ServiceTickets");

            migrationBuilder.DropForeignKey(
                name: "FK_ServiceTickets_SalesInvoices_WarrantySalesInvoiceId",
                table: "ServiceTickets");

            migrationBuilder.DropTable(
                name: "AmcContracts");

            migrationBuilder.DropTable(
                name: "ServiceTicketParts");

            migrationBuilder.DropTable(
                name: "ServiceVisits");

            migrationBuilder.DropIndex(
                name: "IX_ServiceTickets_AmcContractId",
                table: "ServiceTickets");

            migrationBuilder.DropIndex(
                name: "IX_ServiceTickets_ReplacementProductId",
                table: "ServiceTickets");

            migrationBuilder.DropIndex(
                name: "IX_ServiceTickets_WarrantySalesInvoiceId",
                table: "ServiceTickets");

            migrationBuilder.DropColumn(
                name: "AmcContractId",
                table: "ServiceTickets");

            migrationBuilder.DropColumn(
                name: "ReplacementProductId",
                table: "ServiceTickets");

            migrationBuilder.DropColumn(
                name: "ReplacementQuantity",
                table: "ServiceTickets");

            migrationBuilder.DropColumn(
                name: "WarrantyEvidenceNotes",
                table: "ServiceTickets");

            migrationBuilder.DropColumn(
                name: "WarrantySalesInvoiceId",
                table: "ServiceTickets");
        }
    }
}
