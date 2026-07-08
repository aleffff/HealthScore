using HealthScore.Domain;
using Microsoft.EntityFrameworkCore;

namespace HealthScore.Infrastructure;

public sealed class HealthScoreDbContext(DbContextOptions<HealthScoreDbContext> options) : DbContext(options)
{
    public DbSet<AccountRecord> Accounts => Set<AccountRecord>();
    public DbSet<CaseRecord> Cases => Set<CaseRecord>();
    public DbSet<SyncWatermark> SyncWatermarks => Set<SyncWatermark>();
    public DbSet<SyncRun> SyncRuns => Set<SyncRun>();
    public DbSet<EconomicGroup> EconomicGroups => Set<EconomicGroup>();
    public DbSet<BusinessCalendarDay> BusinessCalendar => Set<BusinessCalendarDay>();
    public DbSet<ScoreRuleVersion> ScoreRuleVersions => Set<ScoreRuleVersion>();
    public DbSet<GroupScoreSnapshot> GroupScoreSnapshots => Set<GroupScoreSnapshot>();
    public DbSet<ActionPlan> ActionPlans => Set<ActionPlan>();
    public DbSet<ActionPlanEvent> ActionPlanEvents => Set<ActionPlanEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AccountRecord>(entity =>
        {
            entity.ToTable("accounts");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.SalesforceId).HasMaxLength(18).IsRequired();
            entity.HasIndex(x => x.SalesforceId).IsUnique();
            entity.Property(x => x.Name).HasMaxLength(255).IsRequired();
            entity.Property(x => x.Cnpj).HasMaxLength(14);
            entity.Property(x => x.CnpjRoot).HasMaxLength(8);
            entity.Property(x => x.ParentSalesforceId).HasMaxLength(18);
            entity.Property(x => x.ParentName).HasMaxLength(255);
            entity.Property(x => x.ReportedEconomicGroup).HasMaxLength(255);
            entity.Property(x => x.EconomicGroup).HasMaxLength(255);
            entity.Property(x => x.Brand).HasMaxLength(255);
            entity.Property(x => x.Vertical).HasMaxLength(50).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(100);
            entity.HasIndex(x => x.EconomicGroup);
            entity.HasIndex(x => x.Cnpj);
            entity.HasIndex(x => x.CnpjRoot);
            entity.HasIndex(x => x.ParentSalesforceId);
        });

        modelBuilder.Entity<CaseRecord>(entity =>
        {
            entity.ToTable("cases");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.SalesforceId).HasMaxLength(18).IsRequired();
            entity.HasIndex(x => x.SalesforceId).IsUnique();
            entity.Property(x => x.CaseNumber).HasMaxLength(30).IsRequired();
            entity.Property(x => x.AccountSalesforceId).HasMaxLength(18);
            entity.Property(x => x.ReportedEconomicGroup).HasMaxLength(255);
            entity.Property(x => x.EconomicGroup).HasMaxLength(255);
            entity.Property(x => x.Brand).HasMaxLength(255);
            entity.Property(x => x.Status).HasMaxLength(100);
            entity.Property(x => x.Priority).HasMaxLength(100);
            entity.Property(x => x.JiraIssueCode).HasMaxLength(255);
            entity.Property(x => x.JiraIssueType).HasMaxLength(100);
            entity.Property(x => x.Product).HasMaxLength(255);
            entity.Property(x => x.OpeningVertical).HasMaxLength(100);
            entity.Property(x => x.TaxonomyLevel1).HasMaxLength(500);
            entity.Property(x => x.TaxonomyLevel2).HasMaxLength(500);
            entity.Property(x => x.TaxonomyLevel3).HasMaxLength(500);
            entity.Property(x => x.TaxonomyLevel4).HasMaxLength(500);
            entity.Property(x => x.TaxonomyDescription).HasMaxLength(1000);
            entity.HasIndex(x => x.AccountSalesforceId);
            entity.HasIndex(x => new { x.EconomicGroup, x.SalesforceCreatedAt });
            entity.HasIndex(x => new { x.Brand, x.SalesforceCreatedAt });
            entity.HasIndex(x => new { x.Product, x.SalesforceCreatedAt });
            entity.HasIndex(x => new { x.OpeningVertical, x.SalesforceCreatedAt });
            entity.HasIndex(x => new { x.JiraIssueCode, x.SalesforceCreatedAt });
        });

        modelBuilder.Entity<SyncWatermark>(entity =>
        {
            entity.ToTable("sync_watermarks");
            entity.HasKey(x => x.EntityName);
            entity.Property(x => x.EntityName).HasMaxLength(50);
        });

        modelBuilder.Entity<SyncRun>(entity =>
        {
            entity.ToTable("sync_runs");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.EntityName).HasMaxLength(50).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(30).IsRequired();
            entity.Property(x => x.Error).HasMaxLength(2000);
            entity.HasIndex(x => x.StartedAt);
        });

        modelBuilder.Entity<EconomicGroup>(entity =>
        {
            entity.ToTable("economic_groups");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(255).IsRequired();
            entity.HasIndex(x => x.Name).IsUnique();
        });

        modelBuilder.Entity<BusinessCalendarDay>(entity =>
        {
            entity.ToTable("business_calendar");
            entity.HasKey(x => x.Date);
            entity.Property(x => x.HolidayName).HasMaxLength(255);
        });

        modelBuilder.Entity<ScoreRuleVersion>(entity =>
        {
            entity.ToTable("score_rule_versions");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(30).IsRequired();
            entity.Property(x => x.ConfigurationJson).HasColumnType("longtext").IsRequired();
            entity.Property(x => x.CreatedBy).HasMaxLength(150).IsRequired();
            entity.Property(x => x.Justification).HasMaxLength(1000).IsRequired();
        });

        modelBuilder.Entity<GroupScoreSnapshot>(entity =>
        {
            entity.ToTable("group_score_snapshots");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.EconomicGroup).HasMaxLength(255).IsRequired();
            entity.Property(x => x.SnapshotKind).HasMaxLength(30).IsRequired();
            entity.Property(x => x.RiskBand).HasMaxLength(30).IsRequired();
            entity.Property(x => x.MainReason).HasMaxLength(100).IsRequired();
            foreach (var property in new[]
                     {
                         nameof(GroupScoreSnapshot.Density), nameof(GroupScoreSnapshot.AverageDensity),
                         nameof(GroupScoreSnapshot.DensityVsAverage), nameof(GroupScoreSnapshot.SlaViolatedRate),
                         nameof(GroupScoreSnapshot.FcrRate), nameof(GroupScoreSnapshot.IssueRate),
                         nameof(GroupScoreSnapshot.CriticalRate), nameof(GroupScoreSnapshot.RecurrenceRate),
                         nameof(GroupScoreSnapshot.RecentGrowthRate)
                     })
            {
                entity.Property(property).HasPrecision(18, 8);
            }
            entity.HasIndex(x => new { x.SnapshotKind, x.PeriodStart, x.PeriodEndExclusive, x.ScoreRuleVersionId, x.EconomicGroup }).IsUnique();
            entity.HasIndex(x => new { x.SnapshotKind, x.PeriodEndExclusive, x.Score }).IsDescending(false, false, true);
        });

        modelBuilder.Entity<ActionPlan>(entity =>
        {
            entity.ToTable("action_plans");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.EconomicGroup).HasMaxLength(255).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(30).IsRequired();
            entity.Property(x => x.Responsible).HasMaxLength(200);
            entity.Property(x => x.Notes).HasMaxLength(4000);
            entity.HasIndex(x => x.EconomicGroup).IsUnique();
            entity.HasIndex(x => x.Status);
        });

        modelBuilder.Entity<ActionPlanEvent>(entity =>
        {
            entity.ToTable("action_plan_events");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.EventType).HasMaxLength(50).IsRequired();
            entity.Property(x => x.ChangedBy).HasMaxLength(150).IsRequired();
            entity.Property(x => x.PayloadJson).HasColumnType("longtext").IsRequired();
            entity.HasIndex(x => new { x.ActionPlanId, x.CreatedAt });
        });
    }
}
