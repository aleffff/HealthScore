using HealthScore.Infrastructure;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HealthScore.Infrastructure.Migrations
{
    [DbContext(typeof(HealthScoreDbContext))]
    [Migration("20260708144312_RefreshCasesForProductAndSegment")]
    public partial class RefreshCasesForProductAndSegmentCompatibility : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Compatibility migration.
            // This migration id exists in databases created during development, but
            // the effective refresh logic is versioned in 20260708145012.
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
