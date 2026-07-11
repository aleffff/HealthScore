using HealthScore.Infrastructure;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HealthScore.Infrastructure.Migrations
{
    [DbContext(typeof(HealthScoreDbContext))]
    [Migration("20260710125915_AddProductMappings")]
    public partial class AddProductMappingsCompatibility : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Compatibility migration.
            // This migration id exists in databases created during development, but
            // the effective product mapping schema and seed are versioned in
            // 20260710132000 and the corrective seed migrations that follow it.
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
