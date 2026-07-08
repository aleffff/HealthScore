using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HealthScore.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSnapshotKinds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_group_score_snapshots_PeriodEndExclusive_Score",
                table: "group_score_snapshots");

            migrationBuilder.DropIndex(
                name: "IX_group_score_snapshots_PeriodStart_PeriodEndExclusive_ScoreRu~",
                table: "group_score_snapshots");

            migrationBuilder.AddColumn<string>(
                name: "SnapshotKind",
                table: "group_score_snapshots",
                type: "varchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "rolling30")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_group_score_snapshots_SnapshotKind_PeriodEndExclusive_Score",
                table: "group_score_snapshots",
                columns: new[] { "SnapshotKind", "PeriodEndExclusive", "Score" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "IX_group_score_snapshots_SnapshotKind_PeriodStart_PeriodEndExcl~",
                table: "group_score_snapshots",
                columns: new[] { "SnapshotKind", "PeriodStart", "PeriodEndExclusive", "ScoreRuleVersionId", "EconomicGroup" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_group_score_snapshots_SnapshotKind_PeriodEndExclusive_Score",
                table: "group_score_snapshots");

            migrationBuilder.DropIndex(
                name: "IX_group_score_snapshots_SnapshotKind_PeriodStart_PeriodEndExcl~",
                table: "group_score_snapshots");

            migrationBuilder.DropColumn(
                name: "SnapshotKind",
                table: "group_score_snapshots");

            migrationBuilder.CreateIndex(
                name: "IX_group_score_snapshots_PeriodEndExclusive_Score",
                table: "group_score_snapshots",
                columns: new[] { "PeriodEndExclusive", "Score" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_group_score_snapshots_PeriodStart_PeriodEndExclusive_ScoreRu~",
                table: "group_score_snapshots",
                columns: new[] { "PeriodStart", "PeriodEndExclusive", "ScoreRuleVersionId", "EconomicGroup" },
                unique: true);
        }
    }
}
