using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HealthScore.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class OptimizeAnalyticalPeriodQueries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_cases_SalesforceCreatedAt_EconomicGroup",
                table: "cases",
                columns: new[] { "SalesforceCreatedAt", "EconomicGroup" });

            migrationBuilder.CreateIndex(
                name: "IX_accounts_Brand_Status_EconomicGroup_Cnpj",
                table: "accounts",
                columns: new[] { "Brand", "Status", "EconomicGroup", "Cnpj" });

            migrationBuilder.CreateIndex(
                name: "IX_accounts_Status_EconomicGroup_Cnpj",
                table: "accounts",
                columns: new[] { "Status", "EconomicGroup", "Cnpj" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_cases_SalesforceCreatedAt_EconomicGroup",
                table: "cases");

            migrationBuilder.DropIndex(
                name: "IX_accounts_Brand_Status_EconomicGroup_Cnpj",
                table: "accounts");

            migrationBuilder.DropIndex(
                name: "IX_accounts_Status_EconomicGroup_Cnpj",
                table: "accounts");
        }
    }
}
