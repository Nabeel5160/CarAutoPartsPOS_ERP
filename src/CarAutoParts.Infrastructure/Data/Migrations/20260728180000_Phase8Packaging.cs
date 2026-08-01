using CarAutoParts.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarAutoParts.Infrastructure.Data.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260728180000_Phase8Packaging")]
    public partial class Phase8Packaging : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "SetupCompletedAt",
                table: "CompanySettings",
                type: "datetime2",
                nullable: true);

            // Existing installs: skip wizard
            migrationBuilder.Sql(
                "UPDATE CompanySettings SET SetupCompletedAt = SYSUTCDATETIME() WHERE SetupCompletedAt IS NULL AND IsDeleted = 0;");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SetupCompletedAt",
                table: "CompanySettings");
        }
    }
}
