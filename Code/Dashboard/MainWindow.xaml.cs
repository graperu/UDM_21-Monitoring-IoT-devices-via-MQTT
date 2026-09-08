using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Newtonsoft.Json;
using UDM_21.Dashboard.Controllers;
using UDM_21.Dashboard.Models;
using UDM_21.Dashboard.Services;
using UDM_21.Shared;

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
        private bool _allowClose;

        private const int MaxLogLines = 200;

        public MainWindow()
        {
            InitializeComponent();

            _mqttController = new MqttController();
            _historyManager = new TelemetryHistoryManager();

            _mqttController.TelemetryReceived +=
                _historyManager.AddTelemetry;

            DgDevices.ItemsSource = _devices;
            IcDeviceCards.ItemsSource = _devices;
            CmbDevices.ItemsSource = _devices;
            LbConsole.ItemsSource = _logMessages;

            _mqttController.ConnectionStatusChanged += OnConnectionStatusChanged;
            _mqttController.TelemetryReceived += OnTelemetryReceived;
            _mqttController.DeviceStatusReceived += OnDeviceStatusReceived;
            _mqttController.MessageRejected += OnMessageRejected;

            Loaded += MainWindow_Loaded;
            Closing += MainWindow_Closing;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            var savedDevices = _historyManager.GetLatestMessages();
            foreach (var message in savedDevices)
            {
                MessageValidator.TryParseUtcTimestamp(message.Timestamp, out var timestamp);
                var item = new DeviceItem
                {
                    DeviceId = message.DeviceId,
                    DeviceType = message.DeviceType,
                    Location = message.Location,
                    IsOnline = false,
                    LastSeen = timestamp == default
                        ? message.Timestamp
                        : timestamp.ToLocalTime().ToString("G"),
                    LatestTelemetrySummary = message.DataJson
                };
                item.UpdateTelemetryData(message.Data);
                _devices.Add(item);
            }

            LogConsole($"[HISTORY] SQLite: {_historyManager.DatabasePath}");
            LogConsole($"[HISTORY] Đã nạp {savedDevices.Count} thiết bị từ phiên trước.");
            BtnConnect_Click(this, new RoutedEventArgs());
        }

        private async void BtnConnect_Click(object sender, RoutedEventArgs e)
        {
            string host = TxtHost.Text.Trim();

            if (string.IsNullOrWhiteSpace(host))
            {
                ShowError("Broker host không được để trống.");
                return;
            }

            if (!int.TryParse(TxtPort.Text.Trim(), out int port) || port is < 1 or > 65535)
            {
                ShowError("Port phải là số trong khoảng 1..65535.");
                return;
            }

            BtnConnect.IsEnabled = false;
            try
            {
                var settings = new MqttConnectionSettings
                {
                    Host = host,
                    Port = port,
                    UseTls = ChkTls.IsChecked == true,
                    Username = TxtUsername.Text.Trim(),
                    Password = TxtPassword.Password
                };
                var topicRoot = TxtTopicRoot.Text.Trim();

                await _mqttController.ConnectAsync(settings, topicRoot);
                LogConsole(
                    $"[CONFIG] Topic root: {topicRoot}; TLS: {(settings.UseTls ? "bật" : "tắt")}; " +
                    $"Authentication: {(string.IsNullOrWhiteSpace(settings.Username) ? "không" : "có")}");
            }
            catch (OperationCanceledException)
            {
                ShowError("Kết nối MQTT quá thời gian chờ 10 giây.");
            }
            catch (Exception ex)
            {
                AppLogger.Error("DASHBOARD_CONNECT_FAILED", ex);
                ShowError($"Không thể kết nối MQTT Broker: {ex.Message}");
                BtnConnect.IsEnabled = true;
            }
        }

        private async void BtnDisconnect_Click(object sender, RoutedEventArgs e)
        {
            BtnDisconnect.IsEnabled = false;
            try
            {
                await _mqttController.DisconnectAsync();
            }
            catch (OperationCanceledException)
            {
                ShowError("Ngắt kết nối MQTT quá thời gian chờ.");
            }
            catch (Exception ex)
            {
                AppLogger.Error("DASHBOARD_DISCONNECT_FAILED", ex);
                ShowError($"Không thể ngắt kết nối an toàn: {ex.Message}");
            }
        }

        private void OnConnectionStatusChanged(bool isConnected, string message)
        {
            Dispatcher.BeginInvoke(() =>
            {
                LblStatus.Text = $"Trạng thái: {message}";

                if (isConnected)
                {
                    LblStatus.Foreground = Brushes.Green;
                    BtnConnect.IsEnabled = false;
                    BtnDisconnect.IsEnabled = true;
                    BtnSendCmd.IsEnabled = true;
                    LogConsole($"[SYSTEM] {message}");
                }
                else
                {
                    LblStatus.Foreground = Brushes.Red;
                    BtnConnect.IsEnabled = true;
                    BtnDisconnect.IsEnabled = false;
                    BtnSendCmd.IsEnabled = false;
                    LogConsole($"[SYSTEM] {message}");
                }
            });
        }

        private void OnTelemetryReceived(Shared.TelemetryMessage msg)
        {
            Dispatcher.BeginInvoke(() =>
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

                // Retained status có thể đến trước telemetry và tạo DeviceItem chưa đủ metadata.
                dev.DeviceType = msg.DeviceType;
                dev.Location = msg.Location;
                dev.IsOnline = true;
                dev.LastSeen = DateTime.Now.ToString("T");
                dev.UpdateTelemetryData(msg.Data);

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

                if (DgDevices.SelectedItem is DeviceItem selected && selected.DeviceId == msg.DeviceId)
                {
                    DgHistory.ItemsSource = history;
                }
            });
        }

        private void OnDeviceStatusReceived(
            Shared.DeviceStatusMessage statusMsg)
        {
            Dispatcher.BeginInvoke(() =>
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

        private void OnMessageRejected(string reason)
        {
            Dispatcher.BeginInvoke(() => LogConsole($"[MESSAGE REJECTED] {reason}"));
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
            if (string.IsNullOrWhiteSpace(cmd))
            {
                ShowError("Tên lệnh không được để trống.");
                return;
            }

            BtnSendCmd.IsEnabled = false;
            LblCommandStatus.Text = "Đang gửi lệnh...";
            LblCommandStatus.Foreground = Brushes.DarkOrange;
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
                LblCommandStatus.Text = $"Gửi thành công tới {selectedDev.DeviceId}.";
                LblCommandStatus.Foreground = Brushes.Green;
            }
            catch (Exception ex)
            {
                AppLogger.Error("DASHBOARD_COMMAND_FAILED", ex);
                LblCommandStatus.Text = $"Gửi thất bại: {ex.Message}";
                LblCommandStatus.Foreground = Brushes.Red;
                ShowError($"Không thể gửi lệnh: {ex.Message}");
            }
            finally
            {
                BtnSendCmd.IsEnabled = BtnDisconnect.IsEnabled;
            }
        }

        private void LogConsole(string message)
        {
            Dispatcher.BeginInvoke(() =>
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

            AppLogger.Info("DASHBOARD_EVENT", message);
        }

        private void CmbDevices_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CmbDevices.SelectedItem is not DeviceItem selected) return;

            // Tự động load danh sách mẫu lệnh tương ứng cho từng loại thiết bị
            UpdatePresetCommandsForDevice(selected);
        }

        private void UpdatePresetCommandsForDevice(DeviceItem dev)
        {
            CmbPresetCommands.Items.Clear();

            switch (dev.DeviceId)
            {
                case "smart_light_01":
                    CmbPresetCommands.Items.Add(new PresetCommandItem("💡 Bật đèn (ON)", "TOGGLE_POWER", "{\"state\": \"ON\"}"));
                    CmbPresetCommands.Items.Add(new PresetCommandItem("💡 Tắt đèn (OFF)", "TOGGLE_POWER", "{\"state\": \"OFF\"}"));
                    CmbPresetCommands.Items.Add(new PresetCommandItem("💡 Đảo trạng thái đèn", "TOGGLE_POWER", "{}"));
                    CmbPresetCommands.Items.Add(new PresetCommandItem("☀️ Độ sáng tối đa (100%)", "SET_BRIGHTNESS", "{\"brightness\": 100}"));
                    CmbPresetCommands.Items.Add(new PresetCommandItem("🔅 Độ sáng vừa (50%)", "SET_BRIGHTNESS", "{\"brightness\": 50}"));
                    CmbPresetCommands.Items.Add(new PresetCommandItem("🌑 Tắt độ sáng (0%)", "SET_BRIGHTNESS", "{\"brightness\": 0}"));
                    break;

                case "door_sensor_01":
                    CmbPresetCommands.Items.Add(new PresetCommandItem("🚪 Mở cửa (OPEN)", "OPEN_DOOR", "{}"));
                    CmbPresetCommands.Items.Add(new PresetCommandItem("🚪 Đóng cửa (CLOSED)", "CLOSE_DOOR", "{}"));
                    CmbPresetCommands.Items.Add(new PresetCommandItem("🚪 Đảo trạng thái cửa", "TOGGLE_DOOR", "{}"));
                    CmbPresetCommands.Items.Add(new PresetCommandItem("🚨 Kích hoạt cảnh báo cạy phá", "TRIGGER_ALARM", "{}"));
                    CmbPresetCommands.Items.Add(new PresetCommandItem("🛡️ Hủy cảnh báo cạy phá", "CLEAR_ALARM", "{}"));
                    break;

                case "temp_hum_01":
                    CmbPresetCommands.Items.Add(new PresetCommandItem("❄️ Đặt nhiệt độ mát (22°C)", "SET_TEMPERATURE", "{\"temperature\": 22.0}"));
                    CmbPresetCommands.Items.Add(new PresetCommandItem("🌡️ Đặt nhiệt độ phòng (28°C)", "SET_TEMPERATURE", "{\"temperature\": 28.0}"));
                    CmbPresetCommands.Items.Add(new PresetCommandItem("🔥 Thử nghiệm quá nhiệt (52°C)", "SET_TEMPERATURE", "{\"temperature\": 52.0}"));
                    CmbPresetCommands.Items.Add(new PresetCommandItem("🚨 Kích hoạt cảnh báo nhiệt độ cao", "TRIGGER_HEAT_ALERT", "{}"));
                    CmbPresetCommands.Items.Add(new PresetCommandItem("⚖️ Hiệu chuẩn cảm biến (Calibrate)", "CALIBRATE", "{}"));
                    break;

                case "air_quality_01":
                    CmbPresetCommands.Items.Add(new PresetCommandItem("🍃 Bật lọc khí (AQI 35 - Tốt)", "PURIFY_AIR", "{}"));
                    CmbPresetCommands.Items.Add(new PresetCommandItem("🏭 Giả lập ô nhiễm nhẹ (AQI 85)", "SET_AQI", "{\"aqi\": 85.0}"));
                    CmbPresetCommands.Items.Add(new PresetCommandItem("☣️ Giả lập ô nhiễm nặng (AQI 180)", "SET_AQI", "{\"aqi\": 180.0}"));
                    CmbPresetCommands.Items.Add(new PresetCommandItem("🚨 Kích hoạt cảnh báo chất lượng xấu", "TRIGGER_POLLUTION_ALERT", "{}"));
                    break;

                case "power_meter_01":
                    CmbPresetCommands.Items.Add(new PresetCommandItem("⚡ Đặt tải thông thường (500W)", "SET_LOAD", "{\"power_watt\": 500.0}"));
                    CmbPresetCommands.Items.Add(new PresetCommandItem("⚡ Đặt tải cao (2000W)", "SET_LOAD", "{\"power_watt\": 2000.0}"));
                    CmbPresetCommands.Items.Add(new PresetCommandItem("🔥 Giả lập quá tải lưới (>3500W)", "TRIGGER_OVERLOAD", "{}"));
                    CmbPresetCommands.Items.Add(new PresetCommandItem("🔄 Reset điện năng (0 kWh)", "RESET_ENERGY", "{}"));
                    break;

                default:
                    CmbPresetCommands.Items.Add(new PresetCommandItem("Gửi lệnh tùy chỉnh", "TOGGLE_POWER", "{}"));
                    break;
            }

            if (CmbPresetCommands.Items.Count > 0)
            {
                CmbPresetCommands.SelectedIndex = 0;
            }
        }

        private void CmbPresetCommands_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CmbPresetCommands.SelectedItem is PresetCommandItem preset)
            {
                TxtCommand.Text = preset.Command;
                TxtParams.Text = preset.ParamsJson;
            }
        }

        private async void QuickToggle_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not DeviceItem dev) return;

            string cmdName = "TOGGLE_POWER";
            var cmdParams = new Dictionary<string, object>();

            switch (dev.DeviceId)
            {
                case "smart_light_01":
                    string curLight = "OFF";
                    if (dev.RawTelemetryData.TryGetValue("state", out var st))
                    {
                        curLight = st?.ToString()?.ToUpperInvariant() ?? "OFF";
                    }
                    string newLight = curLight == "ON" ? "OFF" : "ON";
                    cmdName = "TOGGLE_POWER";
                    cmdParams["state"] = newLight;
                    break;

                case "door_sensor_01":
                    cmdName = "TOGGLE_DOOR";
                    break;

                case "temp_hum_01":
                    cmdName = "TRIGGER_HEAT_ALERT";
                    break;

                case "air_quality_01":
                    cmdName = "PURIFY_AIR";
                    break;

                case "power_meter_01":
                    cmdName = "RESET_ENERGY";
                    break;
            }

            try
            {
                await _mqttController.SendCommandAsync(
                    dev.Location,
                    dev.DeviceType,
                    dev.DeviceId,
                    cmdName,
                    cmdParams);

                LogConsole($"[QUICK CMD] Đã gửi lệnh {cmdName} tới {dev.DeviceId}");
            }
            catch (Exception ex)
            {
                ShowError($"Không thể gửi lệnh nhanh: {ex.Message}");
            }
        }

        private void DgDevices_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (DgDevices.SelectedItem is not DeviceItem device) return;

            var historyWindow = new TelemetryHistoryWindow(device.DeviceId, _historyManager)
            {
                Owner = this
            };
            historyWindow.ShowDialog();
            DgHistory.ItemsSource = _historyManager.GetHistory(device.DeviceId);
        }

        private async void MainWindow_Closing(object? sender, CancelEventArgs e)
        {
            if (_allowClose) return;

            e.Cancel = true;
            IsEnabled = false;
            LblStatus.Text = "Trạng thái: Đang đóng kết nối và giải phóng tài nguyên...";

            try
            {
                await _mqttController.DisposeAsync();
            }
            catch (Exception ex)
            {
                AppLogger.Error("DASHBOARD_SHUTDOWN_FAILED", ex);
            }
            finally
            {
                _allowClose = true;
                Close();
            }
        }

        private static void ShowError(string message)
        {
            MessageBox.Show(message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    public class PresetCommandItem
    {
        public string DisplayName { get; set; }
        public string Command { get; set; }
        public string ParamsJson { get; set; }

        public PresetCommandItem(string displayName, string command, string paramsJson)
        {
            DisplayName = displayName;
            Command = command;
            ParamsJson = paramsJson;
        }

        public override string ToString() => DisplayName;
    }
}
