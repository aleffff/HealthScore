using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace HealthScore.Infrastructure;

public sealed class HealthScoreDbContextFactory : IDesignTimeDbContextFactory<HealthScoreDbContext>
{
    public HealthScoreDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<HealthScoreDbContext>()
            .UseMySql(
                "Server=localhost;Port=3307;Database=healthscore;User=healthscore;Password=design-time-only",
                new MariaDbServerVersion(new Version(11, 4, 0)))
            .Options;
        return new HealthScoreDbContext(options);
    }
}
