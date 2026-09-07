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

        public string DeviceId
        {
            get => _deviceId;
            set { _deviceId = value; OnPropertyChanged(); }
        }

        public string Location
        {
            get => _location;
            set { _location = value; OnPropertyChanged(); }
        }

        public string DeviceType
        {
            get => _deviceType;
            set { _deviceType = value; OnPropertyChanged(); }
        }

        public bool IsOnline
        {
            get => _isOnline;
            set { _isOnline = value; OnPropertyChanged(); OnPropertyChanged(nameof(StatusText)); }
        }

        public bool HasWarning
        {
            get => _hasWarning;
            set { _hasWarning = value; OnPropertyChanged(); OnPropertyChanged(nameof(StatusText)); }
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

        public Dictionary<string, object> RawTelemetryData { get; set; } = new Dictionary<string, object>();

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
