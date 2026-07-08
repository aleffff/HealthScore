using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HealthScore.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOpeningBusinessUnit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OpeningBusinessUnit",
                table: "cases",
                type: "varchar(255)",
                maxLength: 255,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_cases_OpeningBusinessUnit_SalesforceCreatedAt",
                table: "cases",
                columns: new[] { "OpeningBusinessUnit", "SalesforceCreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_cases_OpeningBusinessUnit_SalesforceCreatedAt",
                table: "cases");

            migrationBuilder.DropColumn(
                name: "OpeningBusinessUnit",
                table: "cases");
        }
    }
}
