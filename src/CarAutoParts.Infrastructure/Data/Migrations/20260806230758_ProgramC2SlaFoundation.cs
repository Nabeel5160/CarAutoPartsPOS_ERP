using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarAutoParts.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class ProgramC2SlaFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SlaPolicyId",
                table: "ServiceTickets",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BusinessCalendars",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TimeZoneId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    WorkIntervalsJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    HolidaysJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
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
                    table.PrimaryKey("PK_BusinessCalendars", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SlaPolicies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CalendarMode = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_SlaPolicies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SlaTargets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SlaPolicyId = table.Column<int>(type: "int", nullable: false),
                    Metric = table.Column<int>(type: "int", nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    TargetMinutes = table.Column<int>(type: "int", nullable: false),
                    WarnAtPercent = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_SlaTargets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SlaTargets_SlaPolicies_SlaPolicyId",
                        column: x => x.SlaPolicyId,
                        principalTable: "SlaPolicies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SlaTimers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ServiceTicketId = table.Column<int>(type: "int", nullable: false),
                    Metric = table.Column<int>(type: "int", nullable: false),
                    SlaPolicyId = table.Column<int>(type: "int", nullable: false),
                    SlaTargetId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PausedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ActiveSince = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ElapsedSeconds = table.Column<int>(type: "int", nullable: false),
                    TargetSeconds = table.Column<int>(type: "int", nullable: false),
                    WarnSeconds = table.Column<int>(type: "int", nullable: false),
                    WarnedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    BreachedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PauseReason = table.Column<int>(type: "int", nullable: true),
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
                    table.PrimaryKey("PK_SlaTimers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SlaTimers_ServiceTickets_ServiceTicketId",
                        column: x => x.ServiceTicketId,
                        principalTable: "ServiceTickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SlaTimers_SlaPolicies_SlaPolicyId",
                        column: x => x.SlaPolicyId,
                        principalTable: "SlaPolicies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SlaTimers_SlaTargets_SlaTargetId",
                        column: x => x.SlaTargetId,
                        principalTable: "SlaTargets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SlaEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SlaTimerId = table.Column<int>(type: "int", nullable: false),
                    At = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SlaEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SlaEvents_SlaTimers_SlaTimerId",
                        column: x => x.SlaTimerId,
                        principalTable: "SlaTimers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceTickets_SlaPolicyId",
                table: "ServiceTickets",
                column: "SlaPolicyId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessCalendars_CompanyId",
                table: "BusinessCalendars",
                column: "CompanyId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SlaEvents_SlaTimerId",
                table: "SlaEvents",
                column: "SlaTimerId");

            migrationBuilder.CreateIndex(
                name: "IX_SlaPolicies_CompanyId_IsDefault",
                table: "SlaPolicies",
                columns: new[] { "CompanyId", "IsDefault" });

            migrationBuilder.CreateIndex(
                name: "IX_SlaTargets_SlaPolicyId_Metric_Priority",
                table: "SlaTargets",
                columns: new[] { "SlaPolicyId", "Metric", "Priority" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SlaTimers_CompanyId_Status",
                table: "SlaTimers",
                columns: new[] { "CompanyId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_SlaTimers_ServiceTicketId_Metric",
                table: "SlaTimers",
                columns: new[] { "ServiceTicketId", "Metric" },
                unique: true,
                filter: "[IsDeleted] = 0 AND [Status] <> 4");

            migrationBuilder.CreateIndex(
                name: "IX_SlaTimers_SlaPolicyId",
                table: "SlaTimers",
                column: "SlaPolicyId");

            migrationBuilder.CreateIndex(
                name: "IX_SlaTimers_SlaTargetId",
                table: "SlaTimers",
                column: "SlaTargetId");

            migrationBuilder.AddForeignKey(
                name: "FK_ServiceTickets_SlaPolicies_SlaPolicyId",
                table: "ServiceTickets",
                column: "SlaPolicyId",
                principalTable: "SlaPolicies",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ServiceTickets_SlaPolicies_SlaPolicyId",
                table: "ServiceTickets");

            migrationBuilder.DropTable(
                name: "BusinessCalendars");

            migrationBuilder.DropTable(
                name: "SlaEvents");

            migrationBuilder.DropTable(
                name: "SlaTimers");

            migrationBuilder.DropTable(
                name: "SlaTargets");

            migrationBuilder.DropTable(
                name: "SlaPolicies");

            migrationBuilder.DropIndex(
                name: "IX_ServiceTickets_SlaPolicyId",
                table: "ServiceTickets");

            migrationBuilder.DropColumn(
                name: "SlaPolicyId",
                table: "ServiceTickets");
        }
    }
}
