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

        public string DeviceId { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public string DeviceType { get; set; } = string.Empty;

        public bool IsOnline
        {
            get => _isOnline;
            set { _isOnline = value; OnPropertyChanged(); OnPropertyChanged(nameof(StatusText)); }
        }

        public string StatusText => IsOnline ? "Online 🟢" : "Offline 🔴";

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
