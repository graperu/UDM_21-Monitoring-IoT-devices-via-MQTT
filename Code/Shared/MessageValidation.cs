using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using Newtonsoft.Json;

namespace UDM_21.Shared
{
    public static class MessageValidator
    {
        private const int MaxPayloadLength = 64 * 1024;
        private static readonly Regex IdentifierPattern =
            new Regex("^[A-Za-z0-9_-]{1,64}$", RegexOptions.Compiled);

        public static bool TryParseTelemetry(
            string json,
            out TelemetryMessage? message,
            out string error)
        {
            message = null;
            if (!TryValidateJsonInput(json, out error)) return false;

            try
            {
                message = JsonConvert.DeserializeObject<TelemetryMessage>(json);
            }
            catch (JsonException ex)
            {
                error = $"Telemetry JSON không hợp lệ: {ex.Message}";
                return false;
            }

            return ValidateTelemetry(message, out error);
        }

        public static bool TryParseStatus(
            string json,
            out DeviceStatusMessage? message,
            out string error)
        {
            message = null;
            if (!TryValidateJsonInput(json, out error)) return false;

            try
            {
                message = JsonConvert.DeserializeObject<DeviceStatusMessage>(json);
            }
            catch (JsonException ex)
            {
                error = $"Status JSON không hợp lệ: {ex.Message}";
                return false;
            }

            return ValidateStatus(message, out error);
        }

        public static bool TryParseCommand(
            string json,
            out CommandMessage? message,
            out string error)
        {
            message = null;
            if (!TryValidateJsonInput(json, out error)) return false;

            try
            {
                message = JsonConvert.DeserializeObject<CommandMessage>(json);
            }
            catch (JsonException ex)
            {
                error = $"Command JSON không hợp lệ: {ex.Message}";
                return false;
            }

            return ValidateCommand(message, out error);
        }

        public static bool ValidateTelemetry(TelemetryMessage? message, out string error)
        {
            if (message == null)
            {
                error = "Telemetry payload rỗng.";
                return false;
            }

            if (!ValidateCommon(message.MessageId, message.Timestamp, out error)) return false;
            if (!IsValidIdentifier(message.DeviceId))
            {
                error = "device_id không hợp lệ.";
                return false;
            }

            if (!IsValidIdentifier(message.DeviceType))
            {
                error = "device_type không hợp lệ.";
                return false;
            }

            if (!IsValidIdentifier(message.Location))
            {
                error = "location không hợp lệ.";
                return false;
            }

            if (message.Data == null || message.Data.Count == 0)
            {
                error = "data phải chứa ít nhất một giá trị cảm biến.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        public static bool ValidateStatus(DeviceStatusMessage? message, out string error)
        {
            if (message == null)
            {
                error = "Status payload rỗng.";
                return false;
            }

            if (!ValidateCommon(message.MessageId, message.Timestamp, out error)) return false;
            if (!IsValidIdentifier(message.DeviceId))
            {
                error = "device_id không hợp lệ.";
                return false;
            }

            if (!string.Equals(message.Status, "online", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(message.Status, "offline", StringComparison.OrdinalIgnoreCase))
            {
                error = "status chỉ được phép là online hoặc offline.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        public static bool ValidateCommand(CommandMessage? message, out string error)
        {
            if (message == null)
            {
                error = "Command payload rỗng.";
                return false;
            }

            if (!ValidateCommon(message.MessageId, message.Timestamp, out error)) return false;
            if (string.IsNullOrWhiteSpace(message.Command) || message.Command.Length > 64)
            {
                error = "command phải có độ dài từ 1 đến 64 ký tự.";
                return false;
            }

            if (message.Params == null)
            {
                error = "params không được null.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        public static bool ValidateTopic(
            string topic,
            string expectedKind,
            string? deviceId,
            string? deviceType,
            string? location,
            out string error,
            string topicRoot = MqttTopics.DefaultRoot)
        {
            string normalizedRoot;
            try
            {
                normalizedRoot = MqttTopics.NormalizeRoot(topicRoot);
            }
            catch (ArgumentException ex)
            {
                error = ex.Message;
                return false;
            }

            var parts = topic?.Split('/') ?? Array.Empty<string>();
            if (parts.Length != 5 || parts[0] != normalizedRoot || parts[4] != expectedKind)
            {
                error = $"Topic không đúng mẫu {normalizedRoot}/{{location}}/{{device_type}}/{{device_id}}/{expectedKind}.";
                return false;
            }

            if (!IsValidIdentifier(parts[1]) || !IsValidIdentifier(parts[2]) || !IsValidIdentifier(parts[3]))
            {
                error = "Topic chứa định danh không hợp lệ.";
                return false;
            }

            if (deviceId != null && !string.Equals(parts[3], deviceId, StringComparison.Ordinal) ||
                deviceType != null && !string.Equals(parts[2], deviceType, StringComparison.Ordinal) ||
                location != null && !string.Equals(parts[1], location, StringComparison.Ordinal))
            {
                error = "Thông tin thiết bị trong payload không khớp với topic.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        public static bool TryValidateCommandForDevice(
            string deviceType,
            string command,
            IReadOnlyDictionary<string, object> parameters,
            out string error)
        {
            if (!string.Equals(deviceType, "light", StringComparison.OrdinalIgnoreCase))
            {
                error = $"Thiết bị loại '{deviceType}' chưa hỗ trợ lệnh điều khiển.";
                return false;
            }

            if (string.Equals(command, "TOGGLE_POWER", StringComparison.OrdinalIgnoreCase))
            {
                if (parameters.TryGetValue("state", out var stateValue))
                {
                    var state = stateValue?.ToString();
                    if (!string.Equals(state, "ON", StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(state, "OFF", StringComparison.OrdinalIgnoreCase))
                    {
                        error = "state chỉ được phép là ON hoặc OFF.";
                        return false;
                    }
                }

                error = string.Empty;
                return true;
            }

            if (string.Equals(command, "SET_BRIGHTNESS", StringComparison.OrdinalIgnoreCase))
            {
                if (!parameters.TryGetValue("brightness", out var value) ||
                    !int.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), out var brightness) ||
                    brightness < 0 || brightness > 100)
                {
                    error = "brightness phải là số nguyên trong khoảng 0..100.";
                    return false;
                }

                error = string.Empty;
                return true;
            }

            error = $"Lệnh '{command}' không được hỗ trợ cho đèn thông minh.";
            return false;
        }

        public static bool TryParseUtcTimestamp(string value, out DateTimeOffset timestamp)
        {
            if (!DateTimeOffset.TryParse(
                    value,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out timestamp))
            {
                return false;
            }

            return timestamp.Offset == TimeSpan.Zero;
        }

        public static bool IsValidIdentifier(string? value) =>
            !string.IsNullOrWhiteSpace(value) && IdentifierPattern.IsMatch(value);

        private static bool ValidateCommon(string messageId, string timestamp, out string error)
        {
            if (!Guid.TryParse(messageId, out _))
            {
                error = "message_id phải là GUID hợp lệ.";
                return false;
            }

            if (!TryParseUtcTimestamp(timestamp, out _))
            {
                error = "timestamp phải là thời gian ISO-8601 UTC hợp lệ.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static bool TryValidateJsonInput(string json, out string error)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                error = "Payload rỗng.";
                return false;
            }

            if (json.Length > MaxPayloadLength)
            {
                error = $"Payload vượt quá giới hạn {MaxPayloadLength} byte.";
                return false;
            }

            error = string.Empty;
            return true;
        }
    }
}
