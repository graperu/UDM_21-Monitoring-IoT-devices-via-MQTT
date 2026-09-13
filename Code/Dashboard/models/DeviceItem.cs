using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using UDM_21.Dashboard.Services;

namespace UDM_21.Dashboard.Models
{
    public class DeviceItem : INotifyPropertyChanged
    {
        private bool _isOnline;
        private string _lastSeen = "Chưa cập nhật";
        private string _latestTelemetrySummary = "Chưa có dữ liệu";
        private bool _hasWarning;
        private string _warningMessage = string.Empty;
        private string _recommendation = string.Empty;
        private string _deviceId = string.Empty;
        private string _location = string.Empty;
        private string _deviceType = string.Empty;
        private bool _isSelected;

        // Metric fields
        private string _primaryMetricName = "Giá trị";
        private string _primaryValue = "--";
        private string _primaryUnit = "";
        private string _secondaryMetricName = "Chỉ số phụ";
        private string _secondaryValue = "--";

        // Specific device states
        private bool _isLightOn;
        private int _brightness = 100;
        private bool _isDoorOpen;
        private int _batteryPct = 100;
        private bool _tamperAlert;
        private double _aqi;
        private double _temperature;
        private double _humidity;
        private double _powerWatt;
        private double _totalKwh;
        private bool _isDataPulsing;

        public bool IsDataPulsing
        {
            get => _isDataPulsing;
            set { _isDataPulsing = value; OnPropertyChanged(); }
        }

        public ObservableCollection<DeviceMetricTile> DetailedMetrics { get; } = new();

        public string DeviceId
        {
            get => _deviceId;
            set
            {
                _deviceId = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayName));
                OnPropertyChanged(nameof(CategoryName));
                OnPropertyChanged(nameof(VisualCategory));
                UpdateVisuals();
            }
        }

        public string DisplayName => DeviceDisplayNameResolver.GetFriendlyName(DeviceId);
        public string CategoryName => DeviceDisplayNameResolver.GetCategoryName(DeviceId);
        public string LocationFriendly => DeviceDisplayNameResolver.GetLocationFriendly(Location);
        public string VisualCategory => DeviceId switch
        {
            "smart_light_01" => "LIGHT",
            "door_sensor_01" => "DOOR",
            "power_meter_01" => "METER",
            "air_quality_01" => "AIR",
            "temp_hum_01" => "CLIMATE",
            _ => "GENERIC"
        };

        public string Location
        {
            get => _location;
            set
            {
                _location = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(LocationFriendly));
            }
        }

        public string DeviceType
        {
            get => _deviceType;
            set { _deviceType = value; OnPropertyChanged(); }
        }

        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(); }
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
                OnPropertyChanged(nameof(StatusBadgeBg));
                OnPropertyChanged(nameof(StatusBadgeFg));
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
                OnPropertyChanged(nameof(StatusBadgeBg));
                OnPropertyChanged(nameof(StatusBadgeFg));
            }
        }

        public string WarningMessage
        {
            get => _warningMessage;
            set { _warningMessage = value; OnPropertyChanged(); }
        }

        public string Recommendation
        {
            get => _recommendation;
            set { _recommendation = value; OnPropertyChanged(); }
        }

        public string StatusText
        {
            get
            {
                if (!IsOnline) return "Ngoại tuyến";
                if (HasWarning) return "Cảnh báo";
                return "Hoạt động";
            }
        }

        public string ListDisplayValue => DeviceId switch
        {
            "air_quality_01" => $"AQI {PrimaryValue}",
            "door_sensor_01" => IsDoorOpen ? "Đang mở" : "Đã đóng",
            "power_meter_01" => $"{PrimaryValue} W",
            "smart_light_01" => IsLightOn ? $"Bật ({Brightness}%)" : "Tắt",
            "temp_hum_01" => $"{PrimaryValue} °C",
            _ => $"{PrimaryValue} {PrimaryUnit}".Trim()
        };

        public string StatusColor
        {
            get
            {
                if (!IsOnline) return "#DC2626"; // Red
                if (HasWarning) return "#D97706"; // Amber
                return "#16A34A"; // Green
            }
        }

        public string StatusBadgeBg
        {
            get
            {
                if (!IsOnline) return "#FEE2E2";
                if (HasWarning) return "#FEF3C7";
                return "#DCFCE7";
            }
        }

        public string StatusBadgeFg
        {
            get
            {
                if (!IsOnline) return "#B91C1C";
                if (HasWarning) return "#B45309";
                return "#15803D";
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

        public string PrimaryMetricName
        {
            get => _primaryMetricName;
            set { _primaryMetricName = value; OnPropertyChanged(); }
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

        public string SecondaryMetricName
        {
            get => _secondaryMetricName;
            set { _secondaryMetricName = value; OnPropertyChanged(); }
        }

        public string SecondaryValue
        {
            get => _secondaryValue;
            set { _secondaryValue = value; OnPropertyChanged(); }
        }

        // Specific device getters/setters for contextual UI
        public bool IsLightOn
        {
            get => _isLightOn;
            set { _isLightOn = value; OnPropertyChanged(); }
        }

        public int Brightness
        {
            get => _brightness;
            set { _brightness = value; OnPropertyChanged(); }
        }

        public bool IsDoorOpen
        {
            get => _isDoorOpen;
            set { _isDoorOpen = value; OnPropertyChanged(); }
        }

        public int BatteryPct
        {
            get => _batteryPct;
            set { _batteryPct = value; OnPropertyChanged(); }
        }

        public bool TamperAlert
        {
            get => _tamperAlert;
            set { _tamperAlert = value; OnPropertyChanged(); }
        }

        public double Aqi
        {
            get => _aqi;
            set { _aqi = value; OnPropertyChanged(); }
        }

        public double Temperature
        {
            get => _temperature;
            set { _temperature = value; OnPropertyChanged(); }
        }

        public double Humidity
        {
            get => _humidity;
            set { _humidity = value; OnPropertyChanged(); }
        }

        public double PowerWatt
        {
            get => _powerWatt;
            set { _powerWatt = value; OnPropertyChanged(); }
        }

        public double TotalKwh
        {
            get => _totalKwh;
            set { _totalKwh = value; OnPropertyChanged(); }
        }

        public Dictionary<string, object> RawTelemetryData { get; set; } = new();

        public void UpdateTelemetryData(Dictionary<string, object> data)
        {
            RawTelemetryData = data ?? new Dictionary<string, object>();
            UpdateVisuals();

            // Kích hoạt hiệu ứng xung nhịp khi nhận dữ liệu mới từ MQTT
            IsDataPulsing = true;
            System.Threading.Tasks.Task.Delay(500).ContinueWith(_ =>
            {
                System.Windows.Application.Current?.Dispatcher?.BeginInvoke(() => IsDataPulsing = false);
            });
        }

        public void UpdateVisuals()
        {
            DetailedMetrics.Clear();

            switch (DeviceId)
            {
                case "temp_hum_01":
                    PrimaryMetricName = "Nhiệt độ";
                    SecondaryMetricName = "Độ ẩm";
                    if (TryGetDouble("temperature", out var t) && TryGetDouble("humidity", out var h))
                    {
                        Temperature = t;
                        Humidity = h;
                        PrimaryValue = $"{t:F1}";
                        PrimaryUnit = "°C";
                        SecondaryValue = $"{h:F1}%";
                        LatestTelemetrySummary = $"Nhiệt độ: {t:F1}°C | Độ ẩm: {h:F1}%";

                        DetailedMetrics.Add(new DeviceMetricTile("Nhiệt độ", $"{t:F1}", "°C", t > 40 ? "Cảnh báo nhiệt cao" : "", t > 40 ? "#DC2626" : "#2563EB", t > 40));
                        DetailedMetrics.Add(new DeviceMetricTile("Độ ẩm", $"{h:F1}", "%", "", "#0D9488"));
                    }
                    break;

                case "air_quality_01":
                    PrimaryMetricName = "Chỉ số AQI";
                    SecondaryMetricName = "Khí CO₂";
                    if (TryGetDouble("aqi", out var aqi))
                    {
                        Aqi = aqi;
                        PrimaryValue = $"{aqi:F0}";
                        PrimaryUnit = "AQI";
                        string co2Str = TryGetDouble("co2_ppm", out var cVal) ? $"{cVal:F0} ppm" : "--";
                        SecondaryValue = $"CO₂: {co2Str}";

                        string airSt = "Tốt";
                        string airBadgeColor = "#16A34A";
                        if (aqi > 150) { airSt = "Nguy hại"; airBadgeColor = "#DC2626"; }
                        else if (aqi > 100) { airSt = "Kém"; airBadgeColor = "#EA580C"; }
                        else if (aqi > 50) { airSt = "Trung bình"; airBadgeColor = "#D97706"; }

                        LatestTelemetrySummary = $"AQI: {aqi:F0} ({airSt}) | CO₂: {co2Str}";

                        DetailedMetrics.Add(new DeviceMetricTile("AQI", $"{aqi:F0}", "", airSt, airBadgeColor, aqi > 100));
                        DetailedMetrics.Add(new DeviceMetricTile("Khí CO₂", co2Str.Replace(" ppm", ""), "ppm", "", "#059669"));
                        DetailedMetrics.Add(new DeviceMetricTile("Chất lượng", airSt, "", "", airBadgeColor));
                    }
                    break;

                case "power_meter_01":
                    PrimaryMetricName = "Công suất tiêu thụ";
                    SecondaryMetricName = "Điện áp & Dòng";
                    if (TryGetDouble("power_watt", out var pw))
                    {
                        PowerWatt = pw;
                        PrimaryValue = $"{pw:F1}";
                        PrimaryUnit = "W";
                        string v = TryGetDouble("voltage_v", out var vv) ? $"{vv:F1}V" : "--";
                        string i = TryGetDouble("current_a", out var ii) ? $"{ii:F2}A" : "--";
                        string kwh = TryGetDouble("total_kwh", out var kw) ? $"{kw:F2} kWh" : "--";
                        TotalKwh = kw;

                        SecondaryValue = $"{v} • {i}";
                        LatestTelemetrySummary = $"Công suất: {pw:F1}W | {v} | {i} | Điện năng: {kwh}";

                        DetailedMetrics.Add(new DeviceMetricTile("Điện áp", v.Replace("V", ""), "V", "", "#4F46E5"));
                        DetailedMetrics.Add(new DeviceMetricTile("Dòng điện", i.Replace("A", ""), "A", "", "#0891B2"));
                        DetailedMetrics.Add(new DeviceMetricTile("Công suất", $"{pw:F1}", "W", pw > 3000 ? "Quá tải" : "", pw > 3000 ? "#DC2626" : "#2563EB", pw > 3000));
                        DetailedMetrics.Add(new DeviceMetricTile("Điện năng", kwh.Replace(" kWh", ""), "kWh", "", "#16A34A"));
                    }
                    break;

                case "door_sensor_01":
                    PrimaryMetricName = "Trạng thái cửa";
                    SecondaryMetricName = "Pin & Can thiệp";
                    string doorSt = RawTelemetryData.TryGetValue("door_state", out var ds) ? ds?.ToString()?.ToUpperInvariant() ?? "CLOSED" : "CLOSED";
                    IsDoorOpen = doorSt == "OPEN";
                    PrimaryValue = IsDoorOpen ? "ĐANG MỞ" : "ĐÃ ĐÓNG";
                    PrimaryUnit = "";

                    int bat = TryGetInt("battery_pct", out var bVal) ? bVal : 100;
                    BatteryPct = bat;
                    bool tamper = RawTelemetryData.TryGetValue("tamper_alert", out var ta) && ta is bool bTa && bTa;
                    TamperAlert = tamper;

                    SecondaryValue = $"Pin: {bat}% • {(tamper ? "Cạy phá!" : "An toàn")}";
                    LatestTelemetrySummary = $"Cửa: {(IsDoorOpen ? "MỞ" : "ĐÓNG")} | Pin: {bat}% | Cảnh báo: {(tamper ? "CẠY PHÁ" : "Bình thường")}";

                    DetailedMetrics.Add(new DeviceMetricTile("Trạng thái cửa", IsDoorOpen ? "Mở" : "Đóng", "", "", IsDoorOpen ? "#EA580C" : "#16A34A"));
                    DetailedMetrics.Add(new DeviceMetricTile("Mức pin", $"{bat}", "%", bat < 20 ? "Pin yếu" : "", bat < 20 ? "#DC2626" : "#16A34A"));
                    DetailedMetrics.Add(new DeviceMetricTile("Can thiệp", tamper ? "Cạy phá!" : "Không", "", "", tamper ? "#DC2626" : "#16A34A", tamper));
                    break;

                case "smart_light_01":
                    PrimaryMetricName = "Trạng thái đèn";
                    SecondaryMetricName = "Độ sáng & Tải";
                    string lightSt = RawTelemetryData.TryGetValue("state", out var st) ? st?.ToString()?.ToUpperInvariant() ?? "OFF" : "OFF";
                    IsLightOn = lightSt == "ON";
                    PrimaryValue = IsLightOn ? "BẬT" : "TẮT";
                    PrimaryUnit = "";

                    int br = TryGetInt("brightness", out var brVal) ? brVal : (IsLightOn ? 100 : 0);
                    Brightness = br;
                    string pwr = TryGetDouble("power_draw_w", out var pVal2) ? $"{pVal2:F1}W" : (IsLightOn ? "12.0W" : "0.5W");

                    SecondaryValue = $"Độ sáng: {br}% • {pwr}";
                    LatestTelemetrySummary = $"Đèn: {(IsLightOn ? "BẬT" : "TẮT")} | Độ sáng: {br}% | Công suất: {pwr}";

                    DetailedMetrics.Add(new DeviceMetricTile("Trạng thái", IsLightOn ? "Bật" : "Tắt", "", "", IsLightOn ? "#EAB308" : "#64748B"));
                    DetailedMetrics.Add(new DeviceMetricTile("Độ sáng", $"{br}", "%", "", "#CA8A04"));
                    DetailedMetrics.Add(new DeviceMetricTile("Công suất", pwr.Replace("W", ""), "W", "", "#0284C7"));
                    break;

                default:
                    PrimaryMetricName = "Giá trị";
                    SecondaryMetricName = "Thông số";
                    PrimaryValue = "--";
                    PrimaryUnit = "";
                    SecondaryValue = "--";
                    break;
            }

            OnPropertyChanged(nameof(ListDisplayValue));

            OnPropertyChanged(nameof(PrimaryMetricName));
            OnPropertyChanged(nameof(PrimaryValue));
            OnPropertyChanged(nameof(PrimaryUnit));
            OnPropertyChanged(nameof(SecondaryMetricName));
            OnPropertyChanged(nameof(SecondaryValue));
            OnPropertyChanged(nameof(IsLightOn));
            OnPropertyChanged(nameof(Brightness));
            OnPropertyChanged(nameof(IsDoorOpen));
            OnPropertyChanged(nameof(BatteryPct));
            OnPropertyChanged(nameof(TamperAlert));
            OnPropertyChanged(nameof(Aqi));
            OnPropertyChanged(nameof(Temperature));
            OnPropertyChanged(nameof(Humidity));
            OnPropertyChanged(nameof(PowerWatt));
            OnPropertyChanged(nameof(TotalKwh));
            OnPropertyChanged(nameof(LatestTelemetrySummary));
        }

        private bool TryGetDouble(string key, out double result)
        {
            result = 0;
            if (RawTelemetryData.TryGetValue(key, out var val) && val != null)
            {
                return double.TryParse(Convert.ToString(val, CultureInfo.InvariantCulture), NumberStyles.Any, CultureInfo.InvariantCulture, out result);
            }
            return false;
        }

        private bool TryGetInt(string key, out int result)
        {
            result = 0;
            if (RawTelemetryData.TryGetValue(key, out var val) && val != null)
            {
                return int.TryParse(Convert.ToString(val, CultureInfo.InvariantCulture), NumberStyles.Any, CultureInfo.InvariantCulture, out result);
            }
            return false;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
