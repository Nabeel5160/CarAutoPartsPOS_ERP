using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarAutoParts.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class ProgramC1AssignWarrantyWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "WarrantyClaimStatus",
                table: "ServiceTickets",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "WarrantyDecidedAt",
                table: "ServiceTickets",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WarrantyDecidedBy",
                table: "ServiceTickets",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WarrantyDecisionNotes",
                table: "ServiceTickets",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ServiceTickets_CompanyId_IsWarrantyClaim_WarrantyClaimStatus",
                table: "ServiceTickets",
                columns: new[] { "CompanyId", "IsWarrantyClaim", "WarrantyClaimStatus" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ServiceTickets_CompanyId_IsWarrantyClaim_WarrantyClaimStatus",
                table: "ServiceTickets");

            migrationBuilder.DropColumn(
                name: "WarrantyClaimStatus",
                table: "ServiceTickets");

            migrationBuilder.DropColumn(
                name: "WarrantyDecidedAt",
                table: "ServiceTickets");

            migrationBuilder.DropColumn(
                name: "WarrantyDecidedBy",
                table: "ServiceTickets");

            migrationBuilder.DropColumn(
                name: "WarrantyDecisionNotes",
                table: "ServiceTickets");
        }
    }
}
