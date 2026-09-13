using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace UDM_21.Dashboard.Models
{
    public class SystemSummary : INotifyPropertyChanged
    {
        private int _totalDevices = 5;
        private int _onlineCount;
        private int _warningCount;
        private int _offlineCount = 5;
        private bool _isBrokerConnected;
        private string _brokerStatusText = "Chưa kết nối";
        private string _brokerAddress = "broker.emqx.io:1883";
        private string _topicRoot = "udm21_nhom01";

        public int TotalDevices
        {
            get => _totalDevices;
            set { _totalDevices = value; OnPropertyChanged(); }
        }

        public int OnlineCount
        {
            get => _onlineCount;
            set { _onlineCount = value; OnPropertyChanged(); }
        }

        public int WarningCount
        {
            get => _warningCount;
            set { _warningCount = value; OnPropertyChanged(); }
        }

        public int OfflineCount
        {
            get => _offlineCount;
            set { _offlineCount = value; OnPropertyChanged(); }
        }

        public bool IsBrokerConnected
        {
            get => _isBrokerConnected;
            set { _isBrokerConnected = value; OnPropertyChanged(); }
        }

        public string BrokerStatusText
        {
            get => _brokerStatusText;
            set { _brokerStatusText = value; OnPropertyChanged(); }
        }

        public string BrokerAddress
        {
            get => _brokerAddress;
            set { _brokerAddress = value; OnPropertyChanged(); }
        }

        public string TopicRoot
        {
            get => _topicRoot;
            set { _topicRoot = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
