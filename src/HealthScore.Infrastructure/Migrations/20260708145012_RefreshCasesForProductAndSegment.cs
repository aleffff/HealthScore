using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HealthScore.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RefreshCasesForProductAndSegment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Product and scope now come from Produto_Taxonomia__c and Segmento__c.
            // Reset only Case so the configured data period is refreshed idempotently.
            migrationBuilder.Sql("DELETE FROM sync_watermarks WHERE EntityName = 'Case'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
