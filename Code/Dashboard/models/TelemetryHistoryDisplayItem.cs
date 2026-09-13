using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json;
using UDM_21.Dashboard.Services;
using UDM_21.Shared;

namespace UDM_21.Dashboard.Models
{
    public class TelemetryHistoryDisplayItem
    {
        public string MessageId { get; set; } = string.Empty;
        public string DeviceId { get; set; } = string.Empty;
        public string DeviceType { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string LocationFriendly { get; set; } = string.Empty;
        public string TimestampFormatted { get; set; } = string.Empty;
        public string PrimaryValue { get; set; } = string.Empty;
        public string SecondaryValue { get; set; } = string.Empty;
        public string StatusText { get; set; } = "Bình thường";
        public string StatusColor { get; set; } = "#16A34A";
        public string StatusBadgeBg { get; set; } = "#DCFCE7";
        public bool HasWarning { get; set; }
        public string RawJsonFormatted { get; set; } = string.Empty;
        public Dictionary<string, object> Data { get; set; } = new();

        public static TelemetryHistoryDisplayItem FromMessage(TelemetryMessage msg)
        {
            // Chuyển đổi timestamp ISO sang định dạng dễ đọc: HH:mm:ss dd/MM/yyyy
            string formattedTime;
            if (MessageValidator.TryParseUtcTimestamp(msg.Timestamp, out var dt))
            {
                formattedTime = dt.ToLocalTime().ToString("HH:mm:ss dd/MM/yyyy");
            }
            else if (DateTime.TryParse(msg.Timestamp, out var parsedLocal))
            {
                formattedTime = parsedLocal.ToString("HH:mm:ss dd/MM/yyyy");
            }
            else
            {
                formattedTime = msg.Timestamp;
            }

            var item = new TelemetryHistoryDisplayItem
            {
                MessageId = msg.MessageId,
                DeviceId = msg.DeviceId,
                DeviceType = msg.DeviceType,
                DisplayName = DeviceDisplayNameResolver.GetFriendlyName(msg.DeviceId),
                LocationFriendly = DeviceDisplayNameResolver.GetLocationFriendly(msg.Location),
                TimestampFormatted = formattedTime,
                Data = msg.Data ?? new Dictionary<string, object>(),
                RawJsonFormatted = msg.Data != null && msg.Data.Count > 0
                    ? JsonConvert.SerializeObject(msg.Data, Formatting.Indented)
                    : msg.DataJson ?? "{}"
            };

            FormatDeviceSpecificFields(item);
            return item;
        }

        private static void FormatDeviceSpecificFields(TelemetryHistoryDisplayItem item)
        {
            var data = item.Data;
            switch (item.DeviceId)
            {
                case "power_meter_01":
                    double pwr = TryGetDouble(data, "power_watt");
                    double volt = TryGetDouble(data, "voltage_v");
                    double cur = TryGetDouble(data, "current_a");
                    double kwh = TryGetDouble(data, "total_kwh");

                    item.PrimaryValue = $"{pwr:F1} W";
                    item.SecondaryValue = $"{volt:F1} V · {cur:F2} A · {kwh:F2} kWh";
                    if (pwr > 3000.0)
                    {
                        item.StatusText = "Quá tải";
                        item.StatusColor = "#DC2626";
                        item.StatusBadgeBg = "#FEE2E2";
                        item.HasWarning = true;
                    }
                    else
                    {
                        item.StatusText = "Bình thường";
                        item.StatusColor = "#16A34A";
                        item.StatusBadgeBg = "#DCFCE7";
                    }
                    break;

                case "temp_hum_01":
                    double temp = TryGetDouble(data, "temperature");
                    double hum = TryGetDouble(data, "humidity");

                    item.PrimaryValue = $"{temp:F1} °C";
                    item.SecondaryValue = $"{hum:F1} %";
                    if (temp > 40.0)
                    {
                        item.StatusText = "Quá nhiệt";
                        item.StatusColor = "#DC2626";
                        item.StatusBadgeBg = "#FEE2E2";
                        item.HasWarning = true;
                    }
                    else
                    {
                        item.StatusText = "Bình thường";
                        item.StatusColor = "#16A34A";
                        item.StatusBadgeBg = "#DCFCE7";
                    }
                    break;

                case "air_quality_01":
                    double aqi = TryGetDouble(data, "aqi");
                    double co2 = TryGetDouble(data, "co2_ppm");

                    item.PrimaryValue = $"AQI {aqi:F0}";
                    item.SecondaryValue = $"CO₂ {co2:F0} ppm";
                    if (aqi > 150)
                    {
                        item.StatusText = "Nguy hại";
                        item.StatusColor = "#DC2626";
                        item.StatusBadgeBg = "#FEE2E2";
                        item.HasWarning = true;
                    }
                    else if (aqi > 100)
                    {
                        item.StatusText = "Kém";
                        item.StatusColor = "#EA580C";
                        item.StatusBadgeBg = "#FFEDD5";
                        item.HasWarning = true;
                    }
                    else if (aqi > 50)
                    {
                        item.StatusText = "Trung bình";
                        item.StatusColor = "#D97706";
                        item.StatusBadgeBg = "#FEF3C7";
                    }
                    else
                    {
                        item.StatusText = "Tốt";
                        item.StatusColor = "#16A34A";
                        item.StatusBadgeBg = "#DCFCE7";
                    }
                    break;

                case "smart_light_01":
                    string state = TryGetString(data, "state", "OFF").ToUpperInvariant();
                    int bright = TryGetInt(data, "brightness", 0);
                    double power = TryGetDouble(data, "power_draw_w");

                    item.PrimaryValue = state == "ON" ? $"Bật ({bright}%)" : "Tắt";
                    item.SecondaryValue = $"{power:F1} W";
                    item.StatusText = state == "ON" ? "Đang bật" : "Đang tắt";
                    item.StatusColor = state == "ON" ? "#0284C7" : "#64748B";
                    item.StatusBadgeBg = state == "ON" ? "#E0F2FE" : "#F1F5F9";
                    break;

                case "door_sensor_01":
                    string doorState = TryGetString(data, "door_state", "CLOSED").ToUpperInvariant();
                    int bat = TryGetInt(data, "battery_pct", 100);
                    bool tamper = TryGetBool(data, "tamper_alert");

                    item.PrimaryValue = doorState == "OPEN" ? "Đang mở" : "Đã đóng";
                    item.SecondaryValue = $"Pin: {bat}%";
                    if (tamper)
                    {
                        item.StatusText = "Báo động";
                        item.StatusColor = "#DC2626";
                        item.StatusBadgeBg = "#FEE2E2";
                        item.HasWarning = true;
                    }
                    else
                    {
                        item.StatusText = doorState == "OPEN" ? "Cửa mở" : "Bình thường";
                        item.StatusColor = doorState == "OPEN" ? "#D97706" : "#16A34A";
                        item.StatusBadgeBg = doorState == "OPEN" ? "#FEF3C7" : "#DCFCE7";
                    }
                    break;

                default:
                    item.PrimaryValue = "--";
                    item.SecondaryValue = "--";
                    item.StatusText = "Bình thường";
                    break;
            }
        }

        private static double TryGetDouble(Dictionary<string, object> dict, string key)
        {
            if (dict.TryGetValue(key, out var val) && val != null)
            {
                if (double.TryParse(Convert.ToString(val, CultureInfo.InvariantCulture), NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
                    return d;
            }
            return 0;
        }

        private static int TryGetInt(Dictionary<string, object> dict, string key, int def = 0)
        {
            if (dict.TryGetValue(key, out var val) && val != null)
            {
                if (int.TryParse(Convert.ToString(val, CultureInfo.InvariantCulture), NumberStyles.Any, CultureInfo.InvariantCulture, out var i))
                    return i;
            }
            return def;
        }

        private static string TryGetString(Dictionary<string, object> dict, string key, string def = "")
        {
            if (dict.TryGetValue(key, out var val) && val != null)
            {
                return val.ToString() ?? def;
            }
            return def;
        }

        private static bool TryGetBool(Dictionary<string, object> dict, string key)
        {
            if (dict.TryGetValue(key, out var val) && val != null)
            {
                if (bool.TryParse(val.ToString(), out var b)) return b;
            }
            return false;
        }
    }
}
