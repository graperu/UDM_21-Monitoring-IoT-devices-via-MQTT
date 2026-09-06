using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Newtonsoft.Json;
using UDM_21.Dashboard.Controllers;
using UDM_21.Dashboard.Models;
using UDM_21.Dashboard.Services;

namespace UDM_21.Dashboard
{
    public partial class MainWindow : Window
    {
        private readonly MqttController _mqttController;
        private readonly ObservableCollection<DeviceItem> _devices =
            new ObservableCollection<DeviceItem>();
        private readonly ObservableCollection<string> _logMessages =
            new ObservableCollection<string>();
        private readonly TelemetryHistoryManager _historyManager;

        private const int MaxLogLines = 200;

        public MainWindow()
        {
            InitializeComponent();

            _mqttController = new MqttController();
            _historyManager = new TelemetryHistoryManager();

            _mqttController.TelemetryReceived +=
                _historyManager.AddTelemetry;

            DgDevices.ItemsSource = _devices;
            CmbDevices.ItemsSource = _devices;
            LbConsole.ItemsSource = _logMessages;

            _mqttController.ConnectionStatusChanged += OnConnectionStatusChanged;
            _mqttController.TelemetryReceived += OnTelemetryReceived;
            _mqttController.DeviceStatusReceived += OnDeviceStatusReceived;

            Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            BtnConnect_Click(this, new RoutedEventArgs());
        }

        private async void BtnConnect_Click(object sender, RoutedEventArgs e)
        {
            string host = TxtHost.Text.Trim();

            if (int.TryParse(TxtPort.Text.Trim(), out int port))
            {
                await _mqttController.ConnectAsync(host, port);
            }
            else
            {
                MessageBox.Show(
                    "Port không hợp lệ!",
                    "Lỗi",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private async void BtnDisconnect_Click(object sender, RoutedEventArgs e)
        {
            await _mqttController.DisconnectAsync();
        }

        private void OnConnectionStatusChanged(bool isConnected, string message)
        {
            Dispatcher.Invoke(() =>
            {
                LblStatus.Text = $"Trạng thái: {message}";

                if (isConnected)
                {
                    LblStatus.Foreground = Brushes.Green;
                    BtnConnect.IsEnabled = false;
                    BtnDisconnect.IsEnabled = true;
                    LogConsole($"[SYSTEM] {message}");
                }
                else
                {
                    LblStatus.Foreground = Brushes.Red;
                    BtnConnect.IsEnabled = true;
                    BtnDisconnect.IsEnabled = false;
                    LogConsole($"[SYSTEM] {message}");
                }
            });
        }

        private void OnTelemetryReceived(Shared.TelemetryMessage msg)
        {
            Dispatcher.Invoke(() =>
            {
                var dev = _devices.FirstOrDefault(
                    d => d.DeviceId == msg.DeviceId);

                if (dev == null)
                {
                    dev = new DeviceItem
                    {
                        DeviceId = msg.DeviceId,
                        DeviceType = msg.DeviceType,
                        Location = msg.Location,
                        IsOnline = true
                    };

                    _devices.Add(dev);
                }

                dev.IsOnline = true;
                dev.LastSeen = DateTime.Now.ToString("T");
                dev.RawTelemetryData = msg.Data;

                var history =
                    _historyManager.GetHistory(msg.DeviceId);

                dev.LatestTelemetrySummary =
                    JsonConvert.SerializeObject(msg.Data);

                // Kiểm tra ngưỡng bất thường để cảnh báo
                bool isWarning = false;
                string warningDetail = "";

                if (msg.Data.TryGetValue("temperature", out var tempObj) && double.TryParse(tempObj?.ToString(), out double tempVal) && tempVal > 40.0)
                {
                    isWarning = true;
                    warningDetail = $"Nhiệt độ vượt ngưỡng: {tempVal}°C (> 40°C)";
                }
                else if (msg.Data.TryGetValue("power_watt", out var pwrObj) && double.TryParse(pwrObj?.ToString(), out double pwrVal) && pwrVal > 3000.0)
                {
                    isWarning = true;
                    warningDetail = $"Công suất quá tải: {pwrVal}W (> 3000W)";
                }
                else if (msg.Data.TryGetValue("aqi", out var aqiObj) && double.TryParse(aqiObj?.ToString(), out double aqiVal) && aqiVal > 150.0)
                {
                    isWarning = true;
                    warningDetail = $"Chất lượng không khí xấu: AQI {aqiVal} (> 150)";
                }

                dev.HasWarning = isWarning;
                dev.WarningMessage = warningDetail;

                if (isWarning)
                {
                    LogConsole($"🚨 [CẢNH BÁO BẤT THƯỜNG] {msg.DeviceId}: {warningDetail}");
                }
                else
                {
                    LogConsole(
                        $"[TELEMETRY] {msg.DeviceId}: " +
                        $"{dev.LatestTelemetrySummary}");
                }

                LogConsole(
                    $"[HISTORY] {msg.DeviceId}: " +
                    $"Đã lưu {history.Count}/20 dữ liệu gần nhất");
            });
        }

        private void OnDeviceStatusReceived(
            Shared.DeviceStatusMessage statusMsg)
        {
            Dispatcher.Invoke(() =>
            {
                var dev = _devices.FirstOrDefault(
                    d => d.DeviceId == statusMsg.DeviceId);

                if (dev == null)
                {
                    dev = new DeviceItem
                    {
                        DeviceId = statusMsg.DeviceId,
                        IsOnline =
                            statusMsg.Status.ToLower() == "online"
                    };

                    _devices.Add(dev);
                }
                else
                {
                    dev.IsOnline =
                        statusMsg.Status.ToLower() == "online";
                }

                dev.LastSeen = DateTime.Now.ToString("T");

                LogConsole(
                    $"[STATUS] Device {statusMsg.DeviceId} " +
                    $"is {statusMsg.Status.ToUpper()}");
            });
        }

        // =========================================================================
        // SỰ KIỆN CHỌN THIẾT BỊ TRÊN DATAGRID - HIỂN THỊ LỊCH SỬ LÊN UI (ISSUE #8)
        // =========================================================================
        private void DgDevices_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // 1. Kiểm tra sự kiện chọn thiết bị trên DataGrid
            if (DgDevices.SelectedItem is DeviceItem selected)
            {
                // Đồng bộ ComboBox (nếu có)
                if (CmbDevices != null)
                {
                    CmbDevices.SelectedItem = selected;
                }

                // 2. Lấy 20 bản tin lịch sử từ _historyManager dựa vào DeviceId
                var history = _historyManager.GetHistory(selected.DeviceId);

                // 3. Gán danh sách lịch sử vào ItemsSource của DataGrid Lịch sử
                // Trường hợp file XAML đặt tên DataGrid lịch sử khác 'DgHistory', hãy đổi tên ở đây
                if (DgHistory != null)
                {
                    DgHistory.ItemsSource = history;
                }
            }
        }

        private async void BtnSendCmd_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (CmbDevices.SelectedItem is not DeviceItem selectedDev)
            {
                MessageBox.Show(
                    "Vui lòng chọn thiết bị!",
                    "Cảnh báo",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            string cmd = TxtCommand.Text.Trim();

            try
            {
                var paramsDict =
                    JsonConvert.DeserializeObject<
                        Dictionary<string, object>>(
                            TxtParams.Text.Trim())
                    ?? new Dictionary<string, object>();

                await _mqttController.SendCommandAsync(
                    selectedDev.Location,
                    selectedDev.DeviceType,
                    selectedDev.DeviceId,
                    cmd,
                    paramsDict);

                LogConsole(
                    $"[COMMAND SENT] To " +
                    $"{selectedDev.DeviceId}: {cmd}");
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Cú pháp JSON không hợp lệ: {ex.Message}",
                    "Lỗi",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void LogConsole(string message)
        {
            Dispatcher.Invoke(() =>
            {
                string formattedMessage =
                    $"[{DateTime.Now:HH:mm:ss}] {message}";

                _logMessages.Add(formattedMessage);

                if (_logMessages.Count > MaxLogLines)
                {
                    _logMessages.RemoveAt(0);
                }

                if (LbConsole.Items.Count > 0)
                {
                    LbConsole.ScrollIntoView(
                        LbConsole.Items[
                            LbConsole.Items.Count - 1]);
                }
            });
        }
    }
}