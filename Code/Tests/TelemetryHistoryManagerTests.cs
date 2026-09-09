using UDM_21.Dashboard.Services;
using UDM_21.Shared;

namespace UDM_21.Tests;

public sealed class TelemetryHistoryManagerTests
{
    [Fact]
    public void HistorySurvivesManagerRestartAndIgnoresDuplicateMessageId()
    {
        var databasePath = CreateTemporaryDatabasePath();
        try
        {
            var message = CreateMessage("persisted-1", "2026-09-07T01:00:00.0000000Z");
            var firstManager = new TelemetryHistoryManager(databasePath);
            firstManager.AddTelemetry(message);
            firstManager.AddTelemetry(message);

            var restartedManager = new TelemetryHistoryManager(databasePath);
            var history = restartedManager.GetHistory("temp_hum_01");

            var saved = Assert.Single(history);
            Assert.Equal("persisted-1", saved.MessageId);
            Assert.Equal(28.5, Convert.ToDouble(saved.Data["temperature"]));
            Assert.Equal("persisted-1", Assert.Single(restartedManager.GetLatestMessages()).MessageId);
        }
        finally
        {
            DeleteSqliteFiles(databasePath);
        }
    }

    [Fact]
    public void HistoryReturnsOnlyTwentyNewestMessagesAndCanBeCleared()
    {
        var databasePath = CreateTemporaryDatabasePath();
        try
        {
            var manager = new TelemetryHistoryManager(databasePath, storedHistoryLimit: 25);
            for (var index = 0; index < 25; index++)
            {
                manager.AddTelemetry(CreateMessage(
                    $"message-{index}",
                    new DateTime(2026, 9, 7, 1, 0, 0, DateTimeKind.Utc).AddSeconds(index).ToString("o")));
            }

            var history = manager.GetHistory("temp_hum_01");
            Assert.Equal(TelemetryHistoryManager.VisibleHistoryLimit, history.Count);
            Assert.Equal("message-5", history[0].MessageId);
            Assert.Equal("message-24", history[^1].MessageId);

            manager.ClearHistory("temp_hum_01");
            Assert.Empty(manager.GetHistory("temp_hum_01"));
        }
        finally
        {
            DeleteSqliteFiles(databasePath);
        }
    }

    private static TelemetryMessage CreateMessage(string id, string timestamp) => new()
    {
        MessageId = id,
        DeviceId = "temp_hum_01",
        DeviceType = "sensor",
        Location = "lab",
        Timestamp = timestamp,
        Data = new Dictionary<string, object> { ["temperature"] = 28.5 }
    };

    private static string CreateTemporaryDatabasePath()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"udm21-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, "telemetry.db");
    }

    private static void DeleteSqliteFiles(string databasePath)
    {
        var directory = Path.GetDirectoryName(databasePath);
        if (directory != null && Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
    }
}
