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

        private readonly string _connectionString;
        private readonly int _storedHistoryLimit;
        private readonly object _lock = new();

        public string DatabasePath { get; }

        public TelemetryHistoryManager(
            string? databasePath = null,
            int storedHistoryLimit = DefaultStoredHistoryLimit)
        {
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
            if (message == null || string.IsNullOrWhiteSpace(message.DeviceId)) return;

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
                        insert.Parameters.AddWithValue("$message_id", message.MessageId);
                        insert.Parameters.AddWithValue("$device_id", message.DeviceId);
                        insert.Parameters.AddWithValue("$device_type", message.DeviceType);
                        insert.Parameters.AddWithValue("$location", message.Location);
                        insert.Parameters.AddWithValue("$timestamp_utc", message.Timestamp);
                        MessageValidator.TryParseUtcTimestamp(message.Timestamp, out var parsedTimestamp);
                        insert.Parameters.AddWithValue("$timestamp_ticks", parsedTimestamp.UtcDateTime.Ticks);
                        insert.Parameters.AddWithValue("$received_at_utc", DateTime.UtcNow.ToString("o"));
                        insert.Parameters.AddWithValue("$data_json", message.DataJson);
                        insert.ExecuteNonQuery();
                    }

                    using (var cleanup = connection.CreateCommand())
                    {
                        cleanup.Transaction = transaction;
                        cleanup.CommandText = """
                            DELETE FROM telemetry_history
                            WHERE device_id = $device_id
                              AND message_id NOT IN (
                                  SELECT message_id
                                  FROM telemetry_history
                                  WHERE device_id = $device_id
                                  ORDER BY timestamp_ticks DESC, received_at_utc DESC
                                  LIMIT $max_rows
                              );
                            """;
                        cleanup.Parameters.AddWithValue("$device_id", message.DeviceId);
                        cleanup.Parameters.AddWithValue("$max_rows", _storedHistoryLimit);
                        cleanup.ExecuteNonQuery();
                    }

                    transaction.Commit();
                }
                catch (SqliteException ex)
                {
                    AppLogger.Error("HISTORY_DATABASE_WRITE_FAILED", ex);
                }
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
                    """;
                command.ExecuteNonQuery();
            }
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
