using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarAutoParts.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class ProgramC2SlaPendingGaps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ApplyToWarrantyOnly",
                table: "SlaPolicies",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "EscalateToUserId",
                table: "SlaPolicies",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SlaPolicies_EscalateToUserId",
                table: "SlaPolicies",
                column: "EscalateToUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_SlaPolicies_Users_EscalateToUserId",
                table: "SlaPolicies",
                column: "EscalateToUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SlaPolicies_Users_EscalateToUserId",
                table: "SlaPolicies");

            migrationBuilder.DropIndex(
                name: "IX_SlaPolicies_EscalateToUserId",
                table: "SlaPolicies");

            migrationBuilder.DropColumn(
                name: "ApplyToWarrantyOnly",
                table: "SlaPolicies");

            migrationBuilder.DropColumn(
                name: "EscalateToUserId",
                table: "SlaPolicies");
        }
    }
}
