using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace UDM_21.Shared
{
    /// <summary>
    /// Thông điệp dữ liệu cảm biến (Telemetry) thiết bị gửi về Broker
    /// Topic: {topic_root}/{location}/{device_type}/{device_id}/telemetry
    /// </summary>
    public class TelemetryMessage
    {
        [JsonProperty("message_id")]
        public string MessageId { get; set; } = Guid.NewGuid().ToString();

        [JsonProperty("device_id")]
        public string DeviceId { get; set; } = string.Empty;

        [JsonProperty("device_type")]
        public string DeviceType { get; set; } = string.Empty;

        [JsonProperty("location")]
        public string Location { get; set; } = string.Empty;

        [JsonProperty("timestamp")]
        public string Timestamp { get; set; } = DateTime.UtcNow.ToString("o");

        [JsonProperty("data")]
        public Dictionary<string, object> Data { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// Thuộc tính dùng riêng cho WPF DataGrid Binding hiển thị chuỗi JSON của Data
        /// </summary>
        [JsonIgnore]
        public string DataJson => Data != null ? JsonConvert.SerializeObject(Data, Formatting.None) : "{}";

        public string ToJson()
        {
            return JsonConvert.SerializeObject(this);
        }

        public static TelemetryMessage? FromJson(string json)
        {
            return MessageValidator.TryParseTelemetry(json, out var message, out _)
                ? message
                : null;
        }
    }

    /// <summary>
    /// Thông điệp trạng thái Online/Offline của thiết bị (Sử dụng Last Will & Testament - LWT)
    /// Topic: {topic_root}/{location}/{device_type}/{device_id}/status
    /// </summary>
    public class DeviceStatusMessage
    {
        [JsonProperty("message_id")]
        public string MessageId { get; set; } = Guid.NewGuid().ToString();

        [JsonProperty("device_id")]
        public string DeviceId { get; set; } = string.Empty;

        [JsonProperty("status")]
        public string Status { get; set; } = "offline"; // "online" hoặc "offline"

        [JsonProperty("timestamp")]
        public string Timestamp { get; set; } = DateTime.UtcNow.ToString("o");

        public string ToJson() => JsonConvert.SerializeObject(this);
        public static DeviceStatusMessage? FromJson(string json) =>
            MessageValidator.TryParseStatus(json, out var message, out _) ? message : null;
    }

    /// <summary>
    /// Thông điệp lệnh điều khiển từ Dashboard gửi về thiết bị
    /// Topic: {topic_root}/{location}/{device_type}/{device_id}/cmd
    /// </summary>
    public class CommandMessage
    {
        [JsonProperty("message_id")]
        public string MessageId { get; set; } = Guid.NewGuid().ToString();

        [JsonProperty("command")]
        public string Command { get; set; } = string.Empty;

        [JsonProperty("timestamp")]
        public string Timestamp { get; set; } = DateTime.UtcNow.ToString("o");

        [JsonProperty("params")]
        public Dictionary<string, object> Params { get; set; } = new Dictionary<string, object>();

        public string ToJson() => JsonConvert.SerializeObject(this);
        public static CommandMessage? FromJson(string json) =>
            MessageValidator.TryParseCommand(json, out var message, out _) ? message : null;
    }
}
