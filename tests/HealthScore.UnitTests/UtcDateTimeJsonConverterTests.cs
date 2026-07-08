using System.Text.Json;
using HealthScore.Api;

namespace HealthScore.UnitTests;

public sealed class UtcDateTimeJsonConverterTests
{
    private static readonly JsonSerializerOptions Options = new() { Converters = { new UtcDateTimeJsonConverter() } };

    [Fact]
    public void Unspecified_database_date_is_serialized_as_utc()
    {
        var value = new DateTime(2026, 7, 7, 21, 31, 45, DateTimeKind.Unspecified);

        var json = JsonSerializer.Serialize(value, Options);

        Assert.Contains("2026-07-07T21:31:45", json);
        Assert.EndsWith("Z\"", json);
    }

    [Fact]
    public void Utc_payload_remains_utc_when_read()
    {
        var value = JsonSerializer.Deserialize<DateTime>("\"2026-07-07T21:31:45.0000000Z\"", Options);

        Assert.Equal(DateTimeKind.Utc, value.Kind);
        Assert.Equal(21, value.Hour);
    }
}
