using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace UDM_21.Dashboard.Models
{
    public class DeviceItem : INotifyPropertyChanged
    {
        private bool _isOnline;
        private string _lastSeen = DateTime.Now.ToString("g");
        private string _latestTelemetrySummary = "Chưa có dữ liệu";

        private bool _hasWarning;
        private string _warningMessage = string.Empty;
        private string _deviceId = string.Empty;
        private string _location = string.Empty;
        private string _deviceType = string.Empty;

        // Visual properties
        private string _primaryValue = "--";
        private string _primaryUnit = "";
        private string _secondaryValue = "--";
        private string _visualState = "NORMAL";
        private string _icon = "📡";
        private string _badgeColor = "#6c757d";

        public string DeviceId
        {
            get => _deviceId;
            set { _deviceId = value; OnPropertyChanged(); UpdateVisuals(); }
        }

        public string Location
        {
            get => _location;
            set { _location = value; OnPropertyChanged(); }
        }

        public string DeviceType
        {
            get => _deviceType;
            set { _deviceType = value; OnPropertyChanged(); UpdateVisuals(); }
        }

        public bool IsOnline
        {
            get => _isOnline;
            set
            {
                _isOnline = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(StatusText));
                OnPropertyChanged(nameof(StatusColor));
            }
        }

        public bool HasWarning
        {
            get => _hasWarning;
            set
            {
                _hasWarning = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(StatusText));
                OnPropertyChanged(nameof(StatusColor));
            }
        }

        public string WarningMessage
        {
            get => _warningMessage;
            set { _warningMessage = value; OnPropertyChanged(); }
        }

        public string StatusText
        {
            get
            {
                if (!IsOnline) return "Offline";
                if (HasWarning) return "⚠️ Cảnh báo";
                return "Online";
            }
        }

        public string StatusColor
        {
            get
            {
                if (!IsOnline) return "#dc3545"; // Red
                if (HasWarning) return "#fd7e14"; // Orange
                return "#28a745"; // Green
            }
        }

        public string LastSeen
        {
            get => _lastSeen;
            set { _lastSeen = value; OnPropertyChanged(); }
        }

        public string LatestTelemetrySummary
        {
            get => _latestTelemetrySummary;
            set { _latestTelemetrySummary = value; OnPropertyChanged(); }
        }

        public string PrimaryValue
        {
            get => _primaryValue;
            set { _primaryValue = value; OnPropertyChanged(); }
        }

        public string PrimaryUnit
        {
            get => _primaryUnit;
            set { _primaryUnit = value; OnPropertyChanged(); }
        }

        public string SecondaryValue
        {
            get => _secondaryValue;
            set { _secondaryValue = value; OnPropertyChanged(); }
        }

        public string VisualState
        {
            get => _visualState;
            set { _visualState = value; OnPropertyChanged(); }
        }

        public string Icon
        {
            get => _icon;
            set { _icon = value; OnPropertyChanged(); }
        }

        public string BadgeColor
        {
            get => _badgeColor;
            set { _badgeColor = value; OnPropertyChanged(); }
        }

        public bool IsControllable => true; // Cả 5 thiết bị đều hỗ trợ lệnh điều khiển / tương tác
        public string ActionButtonText
        {
            get
            {
                switch (DeviceId)
                {
                    case "smart_light_01": return "Bật/Tắt Đèn";
                    case "door_sensor_01": return "Đóng/Mở Cửa";
                    case "temp_hum_01": return "Cảnh Báo Nhiệt";
                    case "air_quality_01": return "Lọc Không Khí";
                    case "power_meter_01": return "Reset Điện";
                    default: return "Gửi Lệnh";
                }
            }
        }

        public Dictionary<string, object> RawTelemetryData { get; set; } = new Dictionary<string, object>();

        public void UpdateTelemetryData(Dictionary<string, object> data)
        {
            RawTelemetryData = data;
            UpdateVisuals();
        }

        public void UpdateVisuals()
        {
            switch (DeviceId)
            {
                case "temp_hum_01":
                    Icon = "🌡️";
                    BadgeColor = "#0d6efd"; // Blue
                    if (RawTelemetryData.TryGetValue("temperature", out var t) &&
                        RawTelemetryData.TryGetValue("humidity", out var h))
                    {
                        PrimaryValue = $"{t:F1}";
                        PrimaryUnit = "°C";
                        SecondaryValue = $"Độ ẩm: {h}%";
                    }
                    break;

                case "air_quality_01":
                    Icon = "🍃";
                    BadgeColor = "#198754"; // Green
                    if (RawTelemetryData.TryGetValue("aqi", out var aqi))
                    {
                        PrimaryValue = $"{aqi:F0}";
                        PrimaryUnit = "AQI";
                        string co2 = RawTelemetryData.TryGetValue("co2_ppm", out var c) ? $"{c:F0} ppm" : "--";
                        string airStatus = RawTelemetryData.TryGetValue("air_status", out var st) ? st?.ToString() ?? "" : "";
                        SecondaryValue = $"CO2: {co2} | {airStatus}";
                    }
                    break;

                case "power_meter_01":
                    Icon = "⚡";
                    BadgeColor = "#ffc107"; // Yellow/Gold
                    if (RawTelemetryData.TryGetValue("power_watt", out var pw))
                    {
                        PrimaryValue = $"{pw:F1}";
                        PrimaryUnit = "W";
                        string v = RawTelemetryData.TryGetValue("voltage_v", out var vv) ? $"{vv:F1}V" : "--";
                        string i = RawTelemetryData.TryGetValue("current_a", out var ii) ? $"{ii:F2}A" : "--";
                        string kwh = RawTelemetryData.TryGetValue("total_kwh", out var kw) ? $"{kw:F2} kWh" : "--";
                        SecondaryValue = $"{v} | {i} | {kwh}";
                    }
                    break;

                case "door_sensor_01":
                    Icon = "🚪";
                    BadgeColor = "#20c997"; // Teal
                    if (RawTelemetryData.TryGetValue("door_state", out var ds))
                    {
                        string doorSt = ds?.ToString()?.ToUpperInvariant() ?? "CLOSED";
                        PrimaryValue = doorSt == "OPEN" ? "ĐANG MỞ" : "ĐÃ ĐÓNG";
                        Icon = doorSt == "OPEN" ? "🔓" : "🚪";
                        PrimaryUnit = "";
                        string bat = RawTelemetryData.TryGetValue("battery_pct", out var bp) ? $"{bp}%" : "--";
                        bool tamper = RawTelemetryData.TryGetValue("tamper_alert", out var ta) && ta is bool bTa && bTa;
                        SecondaryValue = $"Pin: {bat} | Cảnh báo: {(tamper ? "🚨 BẬT" : "An toàn")}";
                    }
                    break;

                case "smart_light_01":
                    Icon = "💡";
                    BadgeColor = "#6f42c1"; // Purple
                    if (RawTelemetryData.TryGetValue("state", out var state))
                    {
                        string st = state?.ToString()?.ToUpperInvariant() ?? "OFF";
                        PrimaryValue = st == "ON" ? "BẬT" : "TẮT";
                        Icon = st == "ON" ? "💡" : "🌑";
                        PrimaryUnit = "";
                        string br = RawTelemetryData.TryGetValue("brightness", out var b) ? $"{b}%" : "--";
                        string pwr = RawTelemetryData.TryGetValue("power_draw_w", out var p) ? $"{p:F1}W" : "--";
                        SecondaryValue = $"Độ sáng: {br} | Công suất: {pwr}";
                    }
                    break;

                default:
                    Icon = "📡";
                    BadgeColor = "#6c757d";
                    break;
            }

            OnPropertyChanged(nameof(PrimaryValue));
            OnPropertyChanged(nameof(PrimaryUnit));
            OnPropertyChanged(nameof(SecondaryValue));
            OnPropertyChanged(nameof(Icon));
            OnPropertyChanged(nameof(BadgeColor));
            OnPropertyChanged(nameof(IsControllable));
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
