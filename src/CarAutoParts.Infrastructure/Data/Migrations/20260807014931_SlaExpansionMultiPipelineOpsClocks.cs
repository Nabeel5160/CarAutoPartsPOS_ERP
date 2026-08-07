using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarAutoParts.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class SlaExpansionMultiPipelineOpsClocks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SlaTimers_ServiceTicketId_Metric",
                table: "SlaTimers");

            migrationBuilder.AlterColumn<int>(
                name: "ServiceTicketId",
                table: "SlaTimers",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<int>(
                name: "EntityId",
                table: "SlaTimers",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "EntityType",
                table: "SlaTimers",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "AppliesToEntityType",
                table: "SlaPolicies",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // Backfill polymorphic keys for existing ticket timers
            migrationBuilder.Sql("""
                UPDATE SlaTimers
                SET EntityType = 0, EntityId = ServiceTicketId
                WHERE ServiceTicketId IS NOT NULL AND (EntityId = 0 OR EntityId IS NULL);
                """);

            migrationBuilder.CreateTable(
                name: "SlaPolicyRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SlaPolicyId = table.Column<int>(type: "int", nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: true),
                    CustomerType = table.Column<int>(type: "int", nullable: true),
                    CustomerId = table.Column<int>(type: "int", nullable: true),
                    IsWarrantyClaim = table.Column<bool>(type: "bit", nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_SlaPolicyRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SlaPolicyRules_SlaPolicies_SlaPolicyId",
                        column: x => x.SlaPolicyId,
                        principalTable: "SlaPolicies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SlaTimers_CompanyId_EntityType_EntityId_Metric",
                table: "SlaTimers",
                columns: new[] { "CompanyId", "EntityType", "EntityId", "Metric" },
                unique: true,
                filter: "[IsDeleted] = 0 AND [Status] <> 4");

            migrationBuilder.CreateIndex(
                name: "IX_SlaTimers_ServiceTicketId",
                table: "SlaTimers",
                column: "ServiceTicketId");

            migrationBuilder.CreateIndex(
                name: "IX_SlaPolicies_CompanyId_AppliesToEntityType",
                table: "SlaPolicies",
                columns: new[] { "CompanyId", "AppliesToEntityType" });

            migrationBuilder.CreateIndex(
                name: "IX_SlaPolicyRules_CompanyId_SlaPolicyId_SortOrder",
                table: "SlaPolicyRules",
                columns: new[] { "CompanyId", "SlaPolicyId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_SlaPolicyRules_SlaPolicyId",
                table: "SlaPolicyRules",
                column: "SlaPolicyId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SlaPolicyRules");

            migrationBuilder.DropIndex(
                name: "IX_SlaTimers_CompanyId_EntityType_EntityId_Metric",
                table: "SlaTimers");

            migrationBuilder.DropIndex(
                name: "IX_SlaTimers_ServiceTicketId",
                table: "SlaTimers");

            migrationBuilder.DropIndex(
                name: "IX_SlaPolicies_CompanyId_AppliesToEntityType",
                table: "SlaPolicies");

            migrationBuilder.DropColumn(
                name: "EntityId",
                table: "SlaTimers");

            migrationBuilder.DropColumn(
                name: "EntityType",
                table: "SlaTimers");

            migrationBuilder.DropColumn(
                name: "AppliesToEntityType",
                table: "SlaPolicies");

            migrationBuilder.AlterColumn<int>(
                name: "ServiceTicketId",
                table: "SlaTimers",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SlaTimers_ServiceTicketId_Metric",
                table: "SlaTimers",
                columns: new[] { "ServiceTicketId", "Metric" },
                unique: true,
                filter: "[IsDeleted] = 0 AND [Status] <> 4");
        }
    }
}
