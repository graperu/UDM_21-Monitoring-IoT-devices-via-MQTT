using UDM_21.Dashboard.Services;
using UDM_21.Shared;

namespace UDM_21.Tests;

public sealed class TelemetryWriteBufferTests
{
    [Fact]
    public async Task BufferPersistsEveryEnqueuedMessageAfterFlush()
    {
        var databasePath = CreateTemporaryDatabasePath();
        try
        {
            var manager = new TelemetryHistoryManager(databasePath);
            await using var buffer = new TelemetryWriteBuffer(
                manager,
                batchSize: 64,
                flushInterval: TimeSpan.FromMilliseconds(30));

            for (var index = 0; index < 500; index++)
            {
                Assert.True(buffer.Enqueue(CreateMessage($"buffered-{index}", BaseTime.AddSeconds(index))));
            }

            await buffer.FlushAsync(TimeSpan.FromSeconds(15));

            var stats = buffer.Stats;
            Assert.Equal(500L, stats.Enqueued);
            Assert.Equal(500L, stats.Written);
            Assert.Equal(0L, stats.Dropped);
            Assert.Equal(0L, stats.Pending);

            Assert.True(stats.BatchCount < 500, $"Số transaction thực tế: {stats.BatchCount}");
            Assert.True(stats.LargestBatch > 1, "Hàng đợi phải gom được nhiều bản tin trong một lô.");

            Assert.Equal(500, manager.GetFilteredHistory("temp_hum_01", limit: 1000).Count);
        }
        finally
        {
            DeleteSqliteFiles(databasePath);
        }
    }

    [Fact]
    public async Task BufferDropsMessagesInsteadOfGrowingWithoutBound()
    {
        var databasePath = CreateTemporaryDatabasePath();
        try
        {
            var manager = new TelemetryHistoryManager(databasePath);
            await using var buffer = new TelemetryWriteBuffer(
                manager,
                capacity: 5,
                batchSize: 1,
                flushInterval: TimeSpan.FromSeconds(10));

            var accepted = 0;
            for (var index = 0; index < 200; index++)
            {
                if (buffer.Enqueue(CreateMessage($"flood-{index}", BaseTime.AddSeconds(index)))) accepted++;
            }

            Assert.True(accepted <= 200);
            Assert.True(buffer.Stats.Dropped > 0, "Hàng đợi đầy phải bỏ bản tin thay vì phình bộ nhớ.");
            Assert.Equal(200L, buffer.Stats.Enqueued + buffer.Stats.Dropped);
        }
        finally
        {
            DeleteSqliteFiles(databasePath);
        }
    }

    [Fact]
    public void BatchWriteIgnoresDuplicateMessageIdsInsideTheSameBatch()
    {
        var databasePath = CreateTemporaryDatabasePath();
        try
        {
            var manager = new TelemetryHistoryManager(databasePath);
            var written = manager.AddTelemetryBatch(new[]
            {
                CreateMessage("dup-1", BaseTime),
                CreateMessage("dup-1", BaseTime.AddSeconds(1)),
                CreateMessage("dup-2", BaseTime.AddSeconds(2))
            });

            Assert.Equal(2, written);
            Assert.Equal(2, manager.GetHistory("temp_hum_01").Count);
        }
        finally
        {
            DeleteSqliteFiles(databasePath);
        }
    }

    [Fact]
    public void DeferredPruneStillEnforcesTheStoredHistoryLimit()
    {
        var databasePath = CreateTemporaryDatabasePath();
        try
        {
            var manager = new TelemetryHistoryManager(databasePath, storedHistoryLimit: 30, pruneInterval: 10);
            var batch = new List<TelemetryMessage>();
            for (var index = 0; index < 120; index++)
            {
                batch.Add(CreateMessage($"prune-{index}", BaseTime.AddSeconds(index)));
            }

            manager.AddTelemetryBatch(batch);
            manager.PrunePendingDevices();

            var remaining = manager.GetFilteredHistory("temp_hum_01", limit: 1000);
            Assert.Equal(30, remaining.Count);
            Assert.Equal("prune-119", remaining[0].MessageId);
        }
        finally
        {
            DeleteSqliteFiles(databasePath);
        }
    }

    private static readonly DateTime BaseTime = new(2026, 9, 7, 1, 0, 0, DateTimeKind.Utc);

    private static TelemetryMessage CreateMessage(string id, DateTime timestampUtc) => new()
    {
        MessageId = id,
        DeviceId = "temp_hum_01",
        DeviceType = "sensor",
        Location = "lab",
        Timestamp = timestampUtc.ToString("o"),
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
