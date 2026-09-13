using System.Linq;
using System.Windows;
using UDM_21.Dashboard.Models;
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

            var friendly = DeviceDisplayNameResolver.GetFriendlyName(_deviceId);
            LblTitle.Text = $"Lịch Sử Đo Lường: {friendly}";
            LblSubtitle.Text = $"Mã thiết bị: {_deviceId} • Cơ sở dữ liệu: {historyManager.DatabasePath}";
            LoadHistory();
        }

        private void LoadHistory()
        {
            var raw = _historyManager.GetFilteredHistory(_deviceId, 100);
            var history = raw.Select(TelemetryHistoryDisplayItem.FromMessage).ToList();
            DgHistory.ItemsSource = history;
            LblFooterInfo.Text = $"Đang hiển thị {history.Count} bản tin đo lường gần nhất.";
        }

        private void HistoryDelete_Click(object sender, RoutedEventArgs e)
        {
            var confirm = MessageBox.Show(this,
                $"Bạn có chắc chắn muốn xóa toàn bộ lịch sử của '{DeviceDisplayNameResolver.GetFriendlyName(_deviceId)}' ({_deviceId})?",
                "Xác nhận xóa", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (confirm == MessageBoxResult.Yes)
            {
                _historyManager.ClearHistory(_deviceId);
                LoadHistory();
                MessageBox.Show(this,
                    $"Đã xóa toàn bộ lịch sử telemetry của {_deviceId}.",
                    "Thông báo",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
