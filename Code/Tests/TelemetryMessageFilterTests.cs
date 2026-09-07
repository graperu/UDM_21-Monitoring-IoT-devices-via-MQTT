using UDM_21.Shared;

namespace UDM_21.Tests;

public class TelemetryMessageFilterTests
{
    [Fact]
    public void RejectsDuplicateMessageId()
    {
        var filter = new TelemetryMessageFilter();
        var message = CreateMessage(DateTimeOffset.UtcNow);

        Assert.True(filter.TryAccept(message, out _));
        Assert.False(filter.TryAccept(message, out var reason));
        Assert.Contains("Duplicate", reason);
    }

    [Fact]
    public void RejectsLateMessageForSameDevice()
    {
        var filter = new TelemetryMessageFilter();
        var now = DateTimeOffset.UtcNow;

        Assert.True(filter.TryAccept(CreateMessage(now), out _));
        Assert.False(filter.TryAccept(CreateMessage(now.AddSeconds(-5)), out var reason));
        Assert.Contains("Out-of-order", reason);
    }

    [Fact]
    public void AcceptsNewerMessageForSameDevice()
    {
        var filter = new TelemetryMessageFilter();
        var now = DateTimeOffset.UtcNow;

        Assert.True(filter.TryAccept(CreateMessage(now), out _));
        Assert.True(filter.TryAccept(CreateMessage(now.AddSeconds(1)), out var reason), reason);
    }

    [Fact]
    public void CommandDeduplicatorRejectsRepeatedCommand()
    {
        var deduplicator = new MessageDeduplicator(2);
        var id = Guid.NewGuid().ToString();

        Assert.True(deduplicator.TryAccept(id));
        Assert.False(deduplicator.TryAccept(id));
    }

    [Fact]
    public void StatusOrderingFilterRejectsLateStatus()
    {
        var filter = new TimestampOrderingFilter();
        var now = DateTimeOffset.UtcNow;

        Assert.True(filter.TryAccept("device_01", now.ToString("O"), out _));
        Assert.False(filter.TryAccept("device_01", now.AddSeconds(-1).ToString("O"), out var reason));
        Assert.Contains("Out-of-order", reason);
    }

    private static TelemetryMessage CreateMessage(DateTimeOffset timestamp) => new()
    {
        MessageId = Guid.NewGuid().ToString(),
        DeviceId = "device_01",
        DeviceType = "sensor",
        Location = "lab",
        Timestamp = timestamp.ToString("O"),
        Data = new Dictionary<string, object> { ["temperature"] = 25.0 }
    };
}
