using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Microsoft.Data.Sqlite;
using Newtonsoft.Json;
using UDM_21.Shared;

namespace UDM_21.Dashboard.Services
{
    public sealed class TelemetryHistoryManager
    {
        public const int VisibleHistoryLimit = 20;
        public const int DefaultStoredHistoryLimit = 10_000;
        public const int MaxQueryLimit = 1_000;

        public const int DefaultPruneInterval = 32;

        private readonly string _connectionString;
        private readonly int _storedHistoryLimit;
        private readonly int _pruneInterval;
        private readonly Dictionary<string, int> _insertsSincePrune = new(StringComparer.Ordinal);
        private readonly object _lock = new();

        public string DatabasePath { get; }

        public TelemetryHistoryManager(
            string? databasePath = null,
            int storedHistoryLimit = DefaultStoredHistoryLimit,
            int pruneInterval = DefaultPruneInterval)
        {
            if (pruneInterval < 1)
                throw new ArgumentOutOfRangeException(nameof(pruneInterval), "pruneInterval phải >= 1.");
            _pruneInterval = pruneInterval;

            if (storedHistoryLimit < VisibleHistoryLimit)
                throw new ArgumentOutOfRangeException(
                    nameof(storedHistoryLimit),
                    $"Giới hạn lưu phải từ {VisibleHistoryLimit} bản tin trở lên.");

            DatabasePath = Path.GetFullPath(databasePath ?? ResolveDefaultDatabasePath());
            _storedHistoryLimit = storedHistoryLimit;
            Directory.CreateDirectory(Path.GetDirectoryName(DatabasePath)!);
            _connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = DatabasePath,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Cache = SqliteCacheMode.Shared,
                Pooling = false
            }.ToString();

            InitializeDatabase();
            AppLogger.Info("HISTORY_DATABASE_READY", $"path={DatabasePath}; max_per_device={_storedHistoryLimit}");
        }

        public void AddTelemetry(TelemetryMessage message)
        {
            if (message == null) return;
            AddTelemetryBatch(new[] { message });
        }

        public int AddTelemetryBatch(IReadOnlyList<TelemetryMessage> messages)
        {
            if (messages == null || messages.Count == 0) return 0;

            var prepared = new List<(TelemetryMessage Message, long Ticks)>(messages.Count);
            var seenInBatch = new HashSet<string>(StringComparer.Ordinal);
            foreach (var message in messages)
            {
                if (message == null || string.IsNullOrWhiteSpace(message.DeviceId)) continue;
                if (string.IsNullOrWhiteSpace(message.MessageId) || !seenInBatch.Add(message.MessageId)) continue;
                if (!MessageValidator.TryParseUtcTimestamp(message.Timestamp, out var parsedTimestamp))
                {
                    AppLogger.Warning("HISTORY_TIMESTAMP_REJECTED", $"device={message.DeviceId}");
                    continue;
                }

                prepared.Add((message, parsedTimestamp.UtcDateTime.Ticks));
            }

            if (prepared.Count == 0) return 0;

            var inserted = 0;
            var devicesTouched = new Dictionary<string, int>(StringComparer.Ordinal);

            lock (_lock)
            {
                try
                {
                    using var connection = OpenConnection();
                    using var transaction = connection.BeginTransaction();
                    using (var insert = connection.CreateCommand())
                    {
                        insert.Transaction = transaction;
                        insert.CommandText = """
                            INSERT OR IGNORE INTO telemetry_history
                                (message_id, device_id, device_type, location, timestamp_utc, timestamp_ticks, received_at_utc, data_json)
                            VALUES
                                ($message_id, $device_id, $device_type, $location, $timestamp_utc, $timestamp_ticks, $received_at_utc, $data_json);
                            """;

                        var pMessageId = insert.Parameters.Add("$message_id", SqliteType.Text);
                        var pDeviceId = insert.Parameters.Add("$device_id", SqliteType.Text);
                        var pDeviceType = insert.Parameters.Add("$device_type", SqliteType.Text);
                        var pLocation = insert.Parameters.Add("$location", SqliteType.Text);
                        var pTimestamp = insert.Parameters.Add("$timestamp_utc", SqliteType.Text);
                        var pTicks = insert.Parameters.Add("$timestamp_ticks", SqliteType.Integer);
                        var pReceivedAt = insert.Parameters.Add("$received_at_utc", SqliteType.Text);
                        var pDataJson = insert.Parameters.Add("$data_json", SqliteType.Text);
                        insert.Prepare();

                        foreach (var (message, ticks) in prepared)
                        {
                            pMessageId.Value = message.MessageId;
                            pDeviceId.Value = message.DeviceId;
                            pDeviceType.Value = message.DeviceType ?? string.Empty;
                            pLocation.Value = message.Location ?? string.Empty;
                            pTimestamp.Value = message.Timestamp;
                            pTicks.Value = ticks;
                            pReceivedAt.Value = DateTime.UtcNow.ToString("o");
                            pDataJson.Value = message.DataJson;

                            if (insert.ExecuteNonQuery() == 0) continue;
                            inserted++;
                            devicesTouched.TryGetValue(message.DeviceId, out var count);
                            devicesTouched[message.DeviceId] = count + 1;
                        }
                    }

                    transaction.Commit();
                }
                catch (SqliteException ex)
                {
                    AppLogger.Error("HISTORY_DATABASE_WRITE_FAILED", ex);
                    return 0;
                }

                var devicesToPrune = new List<string>();
                foreach (var pair in devicesTouched)
                {
                    _insertsSincePrune.TryGetValue(pair.Key, out var pending);
                    pending += pair.Value;
                    if (pending >= _pruneInterval)
                    {
                        devicesToPrune.Add(pair.Key);
                        _insertsSincePrune[pair.Key] = 0;
                    }
                    else
                    {
                        _insertsSincePrune[pair.Key] = pending;
                    }
                }

                if (devicesToPrune.Count > 0) PruneDevicesNoLock(devicesToPrune);
            }

            return inserted;
        }

        public void PrunePendingDevices()
        {
            lock (_lock)
            {
                if (_insertsSincePrune.Count == 0) return;
                var devices = new List<string>();
                foreach (var pair in _insertsSincePrune)
                {
                    if (pair.Value > 0) devices.Add(pair.Key);
                }

                _insertsSincePrune.Clear();
                if (devices.Count > 0) PruneDevicesNoLock(devices);
            }
        }

        private void PruneDevicesNoLock(IReadOnlyList<string> deviceIds)
        {
            try
            {
                using var connection = OpenConnection();
                using var transaction = connection.BeginTransaction();

                using var count = connection.CreateCommand();
                count.Transaction = transaction;
                count.CommandText = "SELECT COUNT(*) FROM telemetry_history WHERE device_id = $device_id;";
                var pCountDevice = count.Parameters.Add("$device_id", SqliteType.Text);

                using var cleanup = connection.CreateCommand();
                cleanup.Transaction = transaction;

                cleanup.CommandText = """
                    DELETE FROM telemetry_history
                    WHERE device_id = $device_id
                      AND rowid NOT IN (
                          SELECT rowid
                          FROM telemetry_history
                          WHERE device_id = $device_id
                          ORDER BY timestamp_ticks DESC, received_at_utc DESC
                          LIMIT $max_rows
                      );
                    """;
                var pCleanupDevice = cleanup.Parameters.Add("$device_id", SqliteType.Text);
                cleanup.Parameters.AddWithValue("$max_rows", _storedHistoryLimit);

                foreach (var deviceId in deviceIds)
                {
                    pCountDevice.Value = deviceId;
                    var rows = Convert.ToInt32(count.ExecuteScalar(), CultureInfo.InvariantCulture);
                    if (rows <= _storedHistoryLimit) continue;

                    pCleanupDevice.Value = deviceId;
                    cleanup.ExecuteNonQuery();
                }

                transaction.Commit();
            }
            catch (SqliteException ex)
            {
                AppLogger.Error("HISTORY_DATABASE_PRUNE_FAILED", ex);
            }
        }

        public List<TelemetryMessage> GetHistory(string deviceId)
        {
            if (string.IsNullOrWhiteSpace(deviceId)) return new List<TelemetryMessage>();

            lock (_lock)
            {
                try
                {
                    using var connection = OpenConnection();
                    using var command = connection.CreateCommand();
                    command.CommandText = """
                        SELECT message_id, device_id, device_type, location, timestamp_utc, data_json
                        FROM telemetry_history
                        WHERE device_id = $device_id
                        ORDER BY timestamp_ticks DESC, received_at_utc DESC
                        LIMIT $limit;
                        """;
                    command.Parameters.AddWithValue("$device_id", deviceId);
                    command.Parameters.AddWithValue("$limit", VisibleHistoryLimit);

                    var newestFirst = new List<TelemetryMessage>();
                    using var reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        var data = JsonConvert.DeserializeObject<Dictionary<string, object>>(reader.GetString(5))
                            ?? new Dictionary<string, object>();
                        newestFirst.Add(new TelemetryMessage
                        {
                            MessageId = reader.GetString(0),
                            DeviceId = reader.GetString(1),
                            DeviceType = reader.GetString(2),
                            Location = reader.GetString(3),
                            Timestamp = reader.GetString(4),
                            Data = data
                        });
                    }

                    newestFirst.Reverse();
                    return newestFirst;
                }
                catch (SqliteException ex)
                {
                    AppLogger.Error("HISTORY_DATABASE_READ_FAILED", ex);
                    return new List<TelemetryMessage>();
                }
                catch (JsonException ex)
                {
                    AppLogger.Error("HISTORY_DATABASE_JSON_FAILED", ex);
                    return new List<TelemetryMessage>();
                }
            }
        }

        public List<TelemetryMessage> GetAllHistory(int limit = 50)
        {
            ValidateQueryLimit(limit);
            lock (_lock)
            {
                try
                {
                    using var connection = OpenConnection();
                    using var command = connection.CreateCommand();
                    command.CommandText = """
                        SELECT message_id, device_id, device_type, location, timestamp_utc, data_json
                        FROM telemetry_history
                        ORDER BY timestamp_ticks DESC, received_at_utc DESC
                        LIMIT $limit;
                        """;
                    command.Parameters.AddWithValue("$limit", limit);

                    var list = new List<TelemetryMessage>();
                    using var reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        list.Add(ReadMessage(reader));
                    }
                    return list;
                }
                catch (Exception ex)
                {
                    AppLogger.Error("HISTORY_DATABASE_ALL_FAILED", ex);
                    return new List<TelemetryMessage>();
                }
            }
        }

        public List<TelemetryMessage> GetFilteredHistory(string? deviceId = null, int limit = 50)
        {
            ValidateQueryLimit(limit);
            if (string.IsNullOrWhiteSpace(deviceId) || deviceId.Equals("ALL", StringComparison.OrdinalIgnoreCase))
            {
                return GetAllHistory(limit);
            }

            lock (_lock)
            {
                try
                {
                    using var connection = OpenConnection();
                    using var command = connection.CreateCommand();
                    command.CommandText = """
                        SELECT message_id, device_id, device_type, location, timestamp_utc, data_json
                        FROM telemetry_history
                        WHERE device_id = $device_id
                        ORDER BY timestamp_ticks DESC, received_at_utc DESC
                        LIMIT $limit;
                        """;
                    command.Parameters.AddWithValue("$device_id", deviceId);
                    command.Parameters.AddWithValue("$limit", limit);

                    var list = new List<TelemetryMessage>();
                    using var reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        list.Add(ReadMessage(reader));
                    }
                    return list;
                }
                catch (Exception ex)
                {
                    AppLogger.Error("HISTORY_DATABASE_FILTER_FAILED", ex);
                    return new List<TelemetryMessage>();
                }
            }
        }

        public int GetCount(string deviceId)
        {
            if (string.IsNullOrWhiteSpace(deviceId)) return 0;

            lock (_lock)
            {
                using var connection = OpenConnection();
                using var command = connection.CreateCommand();
                command.CommandText = """
                    SELECT COUNT(*) FROM (
                        SELECT 1 FROM telemetry_history
                        WHERE device_id = $device_id
                        LIMIT $limit
                    );
                    """;
                command.Parameters.AddWithValue("$device_id", deviceId);
                command.Parameters.AddWithValue("$limit", VisibleHistoryLimit);
                return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
            }
        }

        public List<TelemetryMessage> GetLatestMessages()
        {
            lock (_lock)
            {
                try
                {
                    using var connection = OpenConnection();
                    using var command = connection.CreateCommand();
                    command.CommandText = """
                        WITH ranked AS (
                            SELECT message_id, device_id, device_type, location, timestamp_utc, data_json,
                                   ROW_NUMBER() OVER (
                                       PARTITION BY device_id
                                       ORDER BY timestamp_ticks DESC, received_at_utc DESC
                                   ) AS row_number
                            FROM telemetry_history
                        )
                        SELECT message_id, device_id, device_type, location, timestamp_utc, data_json
                        FROM ranked
                        WHERE row_number = 1
                        ORDER BY device_id;
                        """;

                    var messages = new List<TelemetryMessage>();
                    using var reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        messages.Add(ReadMessage(reader));
                    }

                    return messages;
                }
                catch (SqliteException ex)
                {
                    AppLogger.Error("HISTORY_DATABASE_LATEST_FAILED", ex);
                    return new List<TelemetryMessage>();
                }
                catch (JsonException ex)
                {
                    AppLogger.Error("HISTORY_DATABASE_JSON_FAILED", ex);
                    return new List<TelemetryMessage>();
                }
            }
        }

        public void ClearHistory(string deviceId)
        {
            if (string.IsNullOrWhiteSpace(deviceId)) return;

            lock (_lock)
            {
                using var connection = OpenConnection();
                using var command = connection.CreateCommand();
                command.CommandText = "DELETE FROM telemetry_history WHERE device_id = $device_id;";
                command.Parameters.AddWithValue("$device_id", deviceId);
                command.ExecuteNonQuery();
                _insertsSincePrune.Remove(deviceId);
            }

            AppLogger.Info("HISTORY_CLEARED", $"device={deviceId}");
        }

        public void ClearAll()
        {
            lock (_lock)
            {
                using var connection = OpenConnection();
                using var command = connection.CreateCommand();
                command.CommandText = "DELETE FROM telemetry_history;";
                command.ExecuteNonQuery();
                _insertsSincePrune.Clear();
            }

            AppLogger.Info("HISTORY_CLEARED", "scope=all");
        }

        private void InitializeDatabase()
        {
            lock (_lock)
            {
                using var connection = OpenConnection();
                using var command = connection.CreateCommand();
                command.CommandText = """
                    PRAGMA journal_mode = WAL;
                    PRAGMA synchronous = NORMAL;

                    CREATE TABLE IF NOT EXISTS telemetry_history (
                        message_id TEXT PRIMARY KEY NOT NULL,
                        device_id TEXT NOT NULL,
                        device_type TEXT NOT NULL,
                        location TEXT NOT NULL,
                        timestamp_utc TEXT NOT NULL,
                        timestamp_ticks INTEGER NOT NULL,
                        received_at_utc TEXT NOT NULL,
                        data_json TEXT NOT NULL
                    );

                    CREATE INDEX IF NOT EXISTS idx_telemetry_device_time
                    ON telemetry_history(device_id, timestamp_ticks DESC);

                    CREATE INDEX IF NOT EXISTS idx_telemetry_all_time
                    ON telemetry_history(timestamp_ticks DESC, received_at_utc DESC);
                    """;
                command.ExecuteNonQuery();
            }
        }

        private static void ValidateQueryLimit(int limit)
        {

            if (limit < 1 || limit > MaxQueryLimit)
                throw new ArgumentOutOfRangeException(nameof(limit), $"limit phải trong 1..{MaxQueryLimit}.");
        }

        private SqliteConnection OpenConnection()
        {
            var connection = new SqliteConnection(_connectionString);
            connection.Open();
            return connection;
        }

        private static TelemetryMessage ReadMessage(SqliteDataReader reader)
        {
            var data = JsonConvert.DeserializeObject<Dictionary<string, object>>(reader.GetString(5))
                ?? new Dictionary<string, object>();
            return new TelemetryMessage
            {
                MessageId = reader.GetString(0),
                DeviceId = reader.GetString(1),
                DeviceType = reader.GetString(2),
                Location = reader.GetString(3),
                Timestamp = reader.GetString(4),
                Data = data
            };
        }

        private static string ResolveDefaultDatabasePath()
        {
            var configuredDirectory = Environment.GetEnvironmentVariable("UDM21_DATA_DIR");
            if (!string.IsNullOrWhiteSpace(configuredDirectory))
                return Path.Combine(Path.GetFullPath(configuredDirectory), "telemetry.db");

            var current = new DirectoryInfo(Environment.CurrentDirectory);
            while (current != null)
            {
                if (Directory.Exists(Path.Combine(current.FullName, ".git")) &&
                    Directory.Exists(Path.Combine(current.FullName, "Code")))
                {
                    return Path.Combine(current.FullName, "Extra", "data", "telemetry.db");
                }

                current = current.Parent;
            }

            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "UDM_21",
                "data",
                "telemetry.db");
        }
    }
}
