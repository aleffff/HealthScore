using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HealthScore.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RefreshCasesForFcrFormula : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The source field changed from FCR__c to the calculated FCR_Formula__c.
            // Reset only the Case watermark so the configured lookback is refreshed idempotently.
            migrationBuilder.Sql("DELETE FROM sync_watermarks WHERE EntityName = 'Case'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
