using System.Windows;
using UDM_21.Dashboard.Services;

namespace UDM_21.Dashboard
{
    public partial class TelemetryHistoryWindow : Window
    {
        private readonly string _deviceId;
        private readonly TelemetryHistoryManager _historyManager;

        public TelemetryHistoryWindow(string deviceId, TelemetryHistoryManager historyManager)
        {
            InitializeComponent();
            _deviceId = deviceId;
            _historyManager = historyManager;
            LblTitle.Text = $"20 bản tin gần nhất của {_deviceId}";
            LoadHistory();
        }

        private void LoadHistory()
        {
            DgHistory.ItemsSource = _historyManager.GetHistory(_deviceId);
        }

        private void HistoryDelete_Click(object sender, RoutedEventArgs e)
        {
            _historyManager.ClearHistory(_deviceId);
            LoadHistory();
            MessageBox.Show(
                $"Đã xóa lịch sử telemetry của {_deviceId}.",
                "Lịch sử",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
    }
}
