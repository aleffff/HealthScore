using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HealthScore.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAnalyticalFilterIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_cases_Brand_SalesforceCreatedAt",
                table: "cases",
                columns: new[] { "Brand", "SalesforceCreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_cases_JiraIssueCode_SalesforceCreatedAt",
                table: "cases",
                columns: new[] { "JiraIssueCode", "SalesforceCreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_cases_OpeningVertical_SalesforceCreatedAt",
                table: "cases",
                columns: new[] { "OpeningVertical", "SalesforceCreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_cases_Product_SalesforceCreatedAt",
                table: "cases",
                columns: new[] { "Product", "SalesforceCreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_cases_Brand_SalesforceCreatedAt",
                table: "cases");

            migrationBuilder.DropIndex(
                name: "IX_cases_JiraIssueCode_SalesforceCreatedAt",
                table: "cases");

            migrationBuilder.DropIndex(
                name: "IX_cases_OpeningVertical_SalesforceCreatedAt",
                table: "cases");

            migrationBuilder.DropIndex(
                name: "IX_cases_Product_SalesforceCreatedAt",
                table: "cases");
        }
    }
}
