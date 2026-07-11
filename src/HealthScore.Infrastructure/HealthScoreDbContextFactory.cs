using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace HealthScore.Infrastructure;

public sealed class HealthScoreDbContextFactory : IDesignTimeDbContextFactory<HealthScoreDbContext>
{
    public HealthScoreDbContext CreateDbContext(string[] args)
    {
        var env = LoadDockerEnv();
        var database = Environment.GetEnvironmentVariable("MARIADB_DATABASE")
            ?? env.GetValueOrDefault("MARIADB_DATABASE")
            ?? "healthscore";
        var user = Environment.GetEnvironmentVariable("MARIADB_USER")
            ?? env.GetValueOrDefault("MARIADB_USER")
            ?? "healthscore";
        var password = Environment.GetEnvironmentVariable("MARIADB_PASSWORD")
            ?? env.GetValueOrDefault("MARIADB_PASSWORD")
            ?? "healthscore-local";

        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__MariaDb")
            ?? Environment.GetEnvironmentVariable("ConnectionStrings:MariaDb")
            ?? $"Server=127.0.0.1;Port=3307;Database={database};User={user};Password={password}";

        var options = new DbContextOptionsBuilder<HealthScoreDbContext>()
            .UseMySql(
                connectionString,
                new MariaDbServerVersion(new Version(11, 4, 0)))
            .Options;
        return new HealthScoreDbContext(options);
    }

    private static Dictionary<string, string> LoadDockerEnv()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory is not null)
        {
            var path = Path.Combine(directory.FullName, ".env");
            if (File.Exists(path))
            {
                return File.ReadAllLines(path)
                    .Select(ParseEnvLine)
                    .Where(pair => pair is not null)
                    .ToDictionary(pair => pair!.Value.Key, pair => pair!.Value.Value, StringComparer.OrdinalIgnoreCase);
            }

            directory = directory.Parent;
        }

        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    private static KeyValuePair<string, string>? ParseEnvLine(string line)
    {
        var trimmed = line.Trim();
        if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith('#'))
        {
            return null;
        }

        var separator = trimmed.IndexOf('=');
        if (separator <= 0)
        {
            return null;
        }

        var key = trimmed[..separator].Trim();
        var value = trimmed[(separator + 1)..].Trim().Trim('"', '\'');
        return new KeyValuePair<string, string>(key, value);
    }
}
