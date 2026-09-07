using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace UDM_21.Shared
{
    // =========================================================================
    // TODO (Thành viên 3): ĐIỀU CHỈNH CẤU TRÚC MESSAGE VÀ JSON PROTOCOL
    // =========================================================================

    /// <summary>
    /// Thông điệp dữ liệu cảm biến (Telemetry) thiết bị gửi về Broker
    /// Topic: iot/{location}/{device_type}/{device_id}/telemetry
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
        public string Timestamp { get; set; } = DateTime.Now.ToString("o");

        [JsonProperty("data")]
        public Dictionary<string, object> Data { get; set; } = new Dictionary<string, object>();

        // TODO: Viết hàm chuyển đối tượng sang chuỗi JSON
        public string ToJson()
        {
            return JsonConvert.SerializeObject(this);
        }

        // TODO: Viết hàm chuyển từ chuỗi JSON sang đối tượng
        public static TelemetryMessage? FromJson(string json)
        {
            return JsonConvert.DeserializeObject<TelemetryMessage>(json);
        }
    }

    /// <summary>
    /// Thông điệp trạng thái Online/Offline của thiết bị (Sử dụng Last Will & Testament - LWT)
    /// Topic: iot/{location}/{device_type}/{device_id}/status
    /// </summary>
    public class DeviceStatusMessage
    {
        [JsonProperty("device_id")]
        public string DeviceId { get; set; } = string.Empty;

        [JsonProperty("status")]
        public string Status { get; set; } = "offline"; // "online" hoặc "offline"

        [JsonProperty("timestamp")]
        public string Timestamp { get; set; } = DateTime.Now.ToString("o");

        public string ToJson() => JsonConvert.SerializeObject(this);
        public static DeviceStatusMessage? FromJson(string json) => JsonConvert.DeserializeObject<DeviceStatusMessage>(json);
    }

    /// <summary>
    /// Thông điệp lệnh điều khiển từ Dashboard gửi về thiết bị
    /// Topic: iot/{location}/{device_type}/{device_id}/cmd
    /// </summary>
    public class CommandMessage
    {
        [JsonProperty("message_id")]
        public string MessageId { get; set; } = Guid.NewGuid().ToString();

        [JsonProperty("command")]
        public string Command { get; set; } = string.Empty;

        [JsonProperty("params")]
        public Dictionary<string, object> Params { get; set; } = new Dictionary<string, object>();

        public string ToJson() => JsonConvert.SerializeObject(this);
        public static CommandMessage? FromJson(string json) => JsonConvert.DeserializeObject<CommandMessage>(json);
    }
}
