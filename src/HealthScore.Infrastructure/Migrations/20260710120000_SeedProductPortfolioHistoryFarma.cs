using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;

#nullable disable

namespace HealthScore.Infrastructure.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(HealthScoreDbContext))]
    [Migration("20260710120000_SeedProductPortfolioHistoryFarma")]
    public partial class SeedProductPortfolioHistoryFarma : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                INSERT INTO `product_portfolio_history` (`Product`, `ReferenceMonth`, `ActiveStores`, `UpdatedAt`, `UpdatedBy`) VALUES
                ('BIG SISTEMAS', DATE '2025-01-01', 5105, UTC_TIMESTAMP(6), 'csv:qtde_lojas_ativas'),
                ('BIG SISTEMAS', DATE '2025-02-01', 5124, UTC_TIMESTAMP(6), 'csv:qtde_lojas_ativas'),
                ('BIG SISTEMAS', DATE '2025-03-01', 5159, UTC_TIMESTAMP(6), 'csv:qtde_lojas_ativas'),
                ('BIG SISTEMAS', DATE '2025-04-01', 5154, UTC_TIMESTAMP(6), 'csv:qtde_lojas_ativas'),
                ('BIG SISTEMAS', DATE '2025-05-01', 5130, UTC_TIMESTAMP(6), 'csv:qtde_lojas_ativas'),
                ('BIG SISTEMAS', DATE '2025-06-01', 5081, UTC_TIMESTAMP(6), 'csv:qtde_lojas_ativas'),
                ('BIG SISTEMAS', DATE '2025-07-01', 4999, UTC_TIMESTAMP(6), 'csv:qtde_lojas_ativas'),
                ('BIG SISTEMAS', DATE '2025-08-01', 4966, UTC_TIMESTAMP(6), 'csv:qtde_lojas_ativas'),
                ('BIG SISTEMAS', DATE '2025-09-01', 5006, UTC_TIMESTAMP(6), 'csv:qtde_lojas_ativas'),
                ('BIG SISTEMAS', DATE '2025-10-01', 4936, UTC_TIMESTAMP(6), 'csv:qtde_lojas_ativas'),
                ('BIG SISTEMAS', DATE '2025-11-01', 4920, UTC_TIMESTAMP(6), 'csv:qtde_lojas_ativas'),
                ('BIG SISTEMAS', DATE '2025-12-01', 4917, UTC_TIMESTAMP(6), 'csv:qtde_lojas_ativas'),
                ('BIG SISTEMAS', DATE '2026-01-01', 4935, UTC_TIMESTAMP(6), 'csv:qtde_lojas_ativas'),
                ('BIG SISTEMAS', DATE '2026-02-01', 4848, UTC_TIMESTAMP(6), 'csv:qtde_lojas_ativas'),
                ('BIG SISTEMAS', DATE '2026-03-01', 4852, UTC_TIMESTAMP(6), 'csv:qtde_lojas_ativas'),
                ('BIG SISTEMAS', DATE '2026-04-01', 4821, UTC_TIMESTAMP(6), 'csv:qtde_lojas_ativas'),
                ('BIG SISTEMAS', DATE '2026-05-01', 4808, UTC_TIMESTAMP(6), 'csv:qtde_lojas_ativas'),
                ('BIG SISTEMAS', DATE '2026-06-01', 4734, UTC_TIMESTAMP(6), 'csv:qtde_lojas_ativas'),
                ('BIG SISTEMAS', DATE '2026-07-01', 4745, UTC_TIMESTAMP(6), 'csv:qtde_lojas_ativas'),
                ('FARMA CLOUD', DATE '2025-01-01', 58, UTC_TIMESTAMP(6), 'csv:qtde_lojas_ativas'),
                ('FARMA CLOUD', DATE '2025-02-01', 42, UTC_TIMESTAMP(6), 'csv:qtde_lojas_ativas'),
                ('FARMA CLOUD', DATE '2025-03-01', 40, UTC_TIMESTAMP(6), 'csv:qtde_lojas_ativas'),
                ('FARMA CLOUD', DATE '2025-04-01', 6, UTC_TIMESTAMP(6), 'csv:qtde_lojas_ativas'),
                ('FARMA CLOUD', DATE '2025-05-01', 4, UTC_TIMESTAMP(6), 'csv:qtde_lojas_ativas'),
                ('FARMA CLOUD', DATE '2025-06-01', 3, UTC_TIMESTAMP(6), 'csv:qtde_lojas_ativas'),
                ('FARMA CLOUD', DATE '2025-07-01', 2, UTC_TIMESTAMP(6), 'csv:qtde_lojas_ativas'),
                ('FARMA CLOUD', DATE '2025-08-01', 2, UTC_TIMESTAMP(6), 'csv:qtde_lojas_ativas'),
                ('FARMA CLOUD', DATE '2025-09-01', 2, UTC_TIMESTAMP(6), 'csv:qtde_lojas_ativas'),
                ('FARMA CLOUD', DATE '2025-10-01', 2, UTC_TIMESTAMP(6), 'csv:qtde_lojas_ativas'),
                ('FARMA CLOUD', DATE '2025-11-01', 2, UTC_TIMESTAMP(6), 'csv:qtde_lojas_ativas'),
                ('FARMA CLOUD', DATE '2025-12-01', 2, UTC_TIMESTAMP(6), 'csv:qtde_lojas_ativas'),
                ('FARMA CLOUD', DATE '2026-01-01', 2, UTC_TIMESTAMP(6), 'csv:qtde_lojas_ativas'),
                ('FARMA CLOUD', DATE '2026-02-01', 2, UTC_TIMESTAMP(6), 'csv:qtde_lojas_ativas'),
                ('FARMA CLOUD', DATE '2026-03-01', 2, UTC_TIMESTAMP(6), 'csv:qtde_lojas_ativas'),
                ('FARMA CLOUD', DATE '2026-04-01', 2, UTC_TIMESTAMP(6), 'csv:qtde_lojas_ativas'),
                ('FARMA CLOUD', DATE '2026-05-01', 2, UTC_TIMESTAMP(6), 'csv:qtde_lojas_ativas'),
                ('FARMA CLOUD', DATE '2026-06-01', 2, UTC_TIMESTAMP(6), 'csv:qtde_lojas_ativas'),
                ('FARMA CLOUD', DATE '2026-07-01', 2, UTC_TIMESTAMP(6), 'csv:qtde_lojas_ativas'),
                ('ITEC', DATE '2025-01-01', 1340, UTC_TIMESTAMP(6), 'csv:qtde_lojas_ativas'),
                ('ITEC', DATE '2025-02-01', 1487, UTC_TIMESTAMP(6), 'csv:qtde_lojas_ativas'),
                ('ITEC', DATE '2025-03-01', 1498, UTC_TIMESTAMP(6), 'csv:qtde_lojas_ativas'),
                ('ITEC', DATE '2025-04-01', 1487, UTC_TIMESTAMP(6), 'csv:qtde_lojas_ativas'),
                ('ITEC', DATE '2025-05-01', 1485, UTC_TIMESTAMP(6), 'csv:qtde_lojas_ativas'),
                ('ITEC', DATE '2025-06-01', 1486, UTC_TIMESTAMP(6), 'csv:qtde_lojas_ativas'),
                ('ITEC', DATE '2025-07-01', 1437, UTC_TIMESTAMP(6), 'csv:qtde_lojas_ativas'),
                ('ITEC', DATE '2025-08-01', 1437, UTC_TIMESTAMP(6), 'csv:qtde_lojas_ativas'),
                ('ITEC', DATE '2025-09-01', 1442, UTC_TIMESTAMP(6), 'csv:qtde_lojas_ativas'),
                ('ITEC', DATE '2025-10-01', 1462, UTC_TIMESTAMP(6), 'csv:qtde_lojas_ativas'),
                ('ITEC', DATE '2025-11-01', 1470, UTC_TIMESTAMP(6), 'csv:qtde_lojas_ativas'),
                ('ITEC', DATE '2025-12-01', 1392, UTC_TIMESTAMP(6), 'csv:qtde_lojas_ativas'),
                ('ITEC', DATE '2026-01-01', 1416, UTC_TIMESTAMP(6), 'csv:qtde_lojas_ativas'),
                ('ITEC', DATE '2026-02-01', 1426, UTC_TIMESTAMP(6), 'csv:qtde_lojas_ativas'),
                ('ITEC', DATE '2026-03-01', 1434, UTC_TIMESTAMP(6), 'csv:qtde_lojas_ativas'),
                ('ITEC', DATE '2026-04-01', 1437, UTC_TIMESTAMP(6), 'csv:qtde_lojas_ativas'),
                ('ITEC', DATE '2026-05-01', 1411, UTC_TIMESTAMP(6), 'csv:qtde_lojas_ativas'),
                ('ITEC', DATE '2026-06-01', 1403, UTC_TIMESTAMP(6), 'csv:qtde_lojas_ativas'),
                ('ITEC', DATE '2026-07-01', 1404, UTC_TIMESTAMP(6), 'csv:qtde_lojas_ativas'),
                ('SOFTPHARMA', DATE '2025-01-01', 1255, UTC_TIMESTAMP(6), 'csv:qtde_lojas_ativas'),
                ('SOFTPHARMA', DATE '2025-02-01', 1239, UTC_TIMESTAMP(6), 'csv:qtde_lojas_ativas'),
                ('SOFTPHARMA', DATE '2025-03-01', 1243, UTC_TIMESTAMP(6), 'csv:qtde_lojas_ativas'),
                ('SOFTPHARMA', DATE '2025-04-01', 1210, UTC_TIMESTAMP(6), 'csv:qtde_lojas_ativas'),
                ('SOFTPHARMA', DATE '2025-05-01', 1167, UTC_TIMESTAMP(6), 'csv:qtde_lojas_ativas'),
                ('SOFTPHARMA', DATE '2025-06-01', 1158, UTC_TIMESTAMP(6), 'csv:qtde_lojas_ativas'),
                ('SOFTPHARMA', DATE '2025-07-01', 1128, UTC_TIMESTAMP(6), 'csv:qtde_lojas_ativas'),
                ('SOFTPHARMA', DATE '2025-08-01', 1103, UTC_TIMESTAMP(6), 'csv:qtde_lojas_ativas'),
                ('SOFTPHARMA', DATE '2025-09-01', 1108, UTC_TIMESTAMP(6), 'csv:qtde_lojas_ativas'),
                ('SOFTPHARMA', DATE '2025-10-01', 1078, UTC_TIMESTAMP(6), 'csv:qtde_lojas_ativas'),
                ('SOFTPHARMA', DATE '2025-11-01', 1056, UTC_TIMESTAMP(6), 'csv:qtde_lojas_ativas'),
                ('SOFTPHARMA', DATE '2025-12-01', 1039, UTC_TIMESTAMP(6), 'csv:qtde_lojas_ativas'),
                ('SOFTPHARMA', DATE '2026-01-01', 1039, UTC_TIMESTAMP(6), 'csv:qtde_lojas_ativas'),
                ('SOFTPHARMA', DATE '2026-02-01', 1012, UTC_TIMESTAMP(6), 'csv:qtde_lojas_ativas'),
                ('SOFTPHARMA', DATE '2026-03-01', 1005, UTC_TIMESTAMP(6), 'csv:qtde_lojas_ativas'),
                ('SOFTPHARMA', DATE '2026-04-01', 981, UTC_TIMESTAMP(6), 'csv:qtde_lojas_ativas'),
                ('SOFTPHARMA', DATE '2026-05-01', 970, UTC_TIMESTAMP(6), 'csv:qtde_lojas_ativas'),
                ('SOFTPHARMA', DATE '2026-06-01', 963, UTC_TIMESTAMP(6), 'csv:qtde_lojas_ativas'),
                ('SOFTPHARMA', DATE '2026-07-01', 961, UTC_TIMESTAMP(6), 'csv:qtde_lojas_ativas')
                ON DUPLICATE KEY UPDATE
                    `ActiveStores` = VALUES(`ActiveStores`),
                    `UpdatedAt` = VALUES(`UpdatedAt`),
                    `UpdatedBy` = VALUES(`UpdatedBy`);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DELETE FROM `product_portfolio_history`
                WHERE `UpdatedBy` = 'csv:qtde_lojas_ativas'
                  AND `Product` IN ('BIG SISTEMAS', 'FARMA CLOUD', 'ITEC', 'SOFTPHARMA')
                  AND `ReferenceMonth` BETWEEN DATE '2025-01-01' AND DATE '2026-07-01';
                """);
        }
    }
}
