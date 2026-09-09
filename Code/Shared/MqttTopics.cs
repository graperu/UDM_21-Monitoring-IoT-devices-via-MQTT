using System;

namespace UDM_21.Shared
{
    public static class MqttTopics
    {
        public const string DefaultRoot = "udm21_nhom01";

        public static string NormalizeRoot(string? topicRoot)
        {
            var value = topicRoot?.Trim() ?? string.Empty;
            if (!MessageValidator.IsValidIdentifier(value))
            {
                throw new ArgumentException(
                    "Topic root chỉ được chứa chữ, số, dấu gạch dưới hoặc gạch ngang (1..64 ký tự).",
                    nameof(topicRoot));
            }

            return value;
        }

        public static string TelemetryWildcard(string topicRoot) =>
            $"{NormalizeRoot(topicRoot)}/+/+/+/telemetry";

        public static string StatusWildcard(string topicRoot) =>
            $"{NormalizeRoot(topicRoot)}/+/+/+/status";

        public static string Telemetry(string topicRoot, string location, string deviceType, string deviceId) =>
            Build(topicRoot, location, deviceType, deviceId, "telemetry");

        public static string Status(string topicRoot, string location, string deviceType, string deviceId) =>
            Build(topicRoot, location, deviceType, deviceId, "status");

        public static string Command(string topicRoot, string location, string deviceType, string deviceId) =>
            Build(topicRoot, location, deviceType, deviceId, "cmd");

        private static string Build(
            string topicRoot,
            string location,
            string deviceType,
            string deviceId,
            string kind)
        {
            var root = NormalizeRoot(topicRoot);
            if (!MessageValidator.IsValidIdentifier(location) ||
                !MessageValidator.IsValidIdentifier(deviceType) ||
                !MessageValidator.IsValidIdentifier(deviceId))
            {
                throw new ArgumentException("Topic chứa định danh không hợp lệ.");
            }

            return $"{root}/{location}/{deviceType}/{deviceId}/{kind}";
        }
    }
}
