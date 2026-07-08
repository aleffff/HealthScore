using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HealthScore.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ResolveGroupsByParentAndCnpjRoot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ReportedEconomicGroup",
                table: "cases",
                type: "varchar(255)",
                maxLength: 255,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "CnpjRoot",
                table: "accounts",
                type: "varchar(8)",
                maxLength: 8,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "ParentName",
                table: "accounts",
                type: "varchar(255)",
                maxLength: 255,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "ParentSalesforceId",
                table: "accounts",
                type: "varchar(18)",
                maxLength: 18,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "ReportedEconomicGroup",
                table: "accounts",
                type: "varchar(255)",
                maxLength: 255,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.Sql("UPDATE accounts SET ReportedEconomicGroup = EconomicGroup WHERE ReportedEconomicGroup IS NULL");
            migrationBuilder.Sql("UPDATE cases SET ReportedEconomicGroup = EconomicGroup WHERE ReportedEconomicGroup IS NULL");
            migrationBuilder.Sql("DELETE FROM sync_watermarks WHERE EntityName = 'Account'");

            migrationBuilder.CreateIndex(
                name: "IX_accounts_CnpjRoot",
                table: "accounts",
                column: "CnpjRoot");

            migrationBuilder.CreateIndex(
                name: "IX_accounts_ParentSalesforceId",
                table: "accounts",
                column: "ParentSalesforceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_accounts_CnpjRoot",
                table: "accounts");

            migrationBuilder.DropIndex(
                name: "IX_accounts_ParentSalesforceId",
                table: "accounts");

            migrationBuilder.DropColumn(
                name: "ReportedEconomicGroup",
                table: "cases");

            migrationBuilder.DropColumn(
                name: "CnpjRoot",
                table: "accounts");

            migrationBuilder.DropColumn(
                name: "ParentName",
                table: "accounts");

            migrationBuilder.DropColumn(
                name: "ParentSalesforceId",
                table: "accounts");

            migrationBuilder.DropColumn(
                name: "ReportedEconomicGroup",
                table: "accounts");
        }
    }
}
