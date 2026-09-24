using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Data;
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
        private const int MaxLogLines = 250;
        private readonly MqttController _mqttController = new();
        private readonly ObservableCollection<DeviceItem> _devices = new();
        private readonly ObservableCollection<LogEntry> _logEntries = new();
        private readonly TelemetryHistoryManager _historyManager = new();
        private readonly TelemetryWriteBuffer _historyWriter;
        private long _lastReportedDropCount;
        private DeviceItem? _selectedDevice;
        private bool _allowClose;
        private ListCollectionView? _deviceView;
        private bool _commandBusy;
        private bool _isClosing;
        private bool _historyDirty = true;
        private bool _historyLoading;
        private int _historyRequestVersion;
        private readonly DispatcherTimer _historyRefreshTimer = new()
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };

        private const string ChartDeviceIdTemp = "temp_hum_01";
        private const string ChartDeviceIdPower = "power_meter_01";
        private const string ChartDeviceIdAqi = "air_quality_01";
        private const double ChartThresholdTemp = 40.0;
        private const double ChartThresholdPower = 3000.0;
        private const double ChartThresholdAqi = 100.0;
        private const int ChartMaxBufferedPoints = 200;

        private readonly List<(DateTime Timestamp, double Value)> _chartPointsTemp = new();
        private readonly List<(DateTime Timestamp, double Value)> _chartPointsPower = new();
        private readonly List<(DateTime Timestamp, double Value)> _chartPointsAqi = new();
        private bool _chartsDirty = true;
        private int _chartPointLimit = 50;

        #region 1. KHỞI TẠO GIAO DIỆN & NẠP LỊCH SỬ SQLITE

        public MainWindow()
        {
            InitializeComponent();

            _historyWriter = new TelemetryWriteBuffer(_historyManager);

            _mqttController.TelemetryReceived += OnTelemetryPersistRequested;
            _mqttController.ConnectionStatusChanged += OnConnectionStatusChanged;
            _mqttController.TelemetryReceived += OnTelemetryReceived;
            _mqttController.DeviceStatusReceived += OnDeviceStatusReceived;
            _mqttController.MessageRejected += OnMessageRejected;

            _deviceView = new ListCollectionView(_devices);
            _deviceView.Filter = MatchesDeviceFilter;
            IcDeviceCards.ItemsSource = _deviceView;
            DgDevices.ItemsSource = _devices;
            LbLogEntries.ItemsSource = _logEntries;

            Loaded += MainWindow_Loaded;
            Closing += MainWindow_Closing;
            _historyRefreshTimer.Tick += (_, _) =>
            {
                if (_historyDirty && !_historyLoading && MainTabs.SelectedItem == TabHistory) RefreshHistoryTable();
                if (_chartsDirty && MainTabs.SelectedItem == TabCharts) RenderCharts();
            };
            _historyRefreshTimer.Start();
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {

            var defaultDeviceSpecs = new[]
            {
                ("air_quality_01", "sensor", "factory"),
                ("door_sensor_01", "security", "lab"),
                ("power_meter_01", "meter", "home"),
                ("smart_light_01", "light", "home"),
                ("temp_hum_01", "sensor", "lab")
            };

            var savedDevices = _historyManager.GetLatestMessages();
            var savedDict = savedDevices.ToDictionary(d => d.DeviceId, d => d);

            foreach (var spec in defaultDeviceSpecs)
            {
                var item = new DeviceItem
                {
                    DeviceId = spec.Item1,
                    DeviceType = spec.Item2,
                    Location = spec.Item3,
                    IsOnline = false,
                    LastSeen = "Chờ tín hiệu..."
                };

                if (savedDict.TryGetValue(spec.Item1, out var savedMsg))
                {
                    MessageValidator.TryParseUtcTimestamp(savedMsg.Timestamp, out var timestamp);
                    item.LastSeen = timestamp == default ? savedMsg.Timestamp : timestamp.ToLocalTime().ToString("G");
                    item.UpdateTelemetryData(savedMsg.Data);
                }
                else
                {
                    item.UpdateVisuals();
                }

                _devices.Add(item);
            }

            var filterOptions = new List<HistoryDeviceFilterOption>
            {
                new() { DisplayText = "Tất cả thiết bị", DeviceId = null },
                new() { DisplayText = "Cảm biến chất lượng không khí", DeviceId = "air_quality_01" },
                new() { DisplayText = "Cảm biến cửa", DeviceId = "door_sensor_01" },
                new() { DisplayText = "Công tơ điện", DeviceId = "power_meter_01" },
                new() { DisplayText = "Đèn thông minh", DeviceId = "smart_light_01" },
                new() { DisplayText = "Nhiệt độ & độ ẩm", DeviceId = "temp_hum_01" }
            };
            CmbHistoryFilter.ItemsSource = filterOptions;
            CmbHistoryFilter.DisplayMemberPath = "DisplayText";
            CmbHistoryFilter.SelectedIndex = 0;

            if (_devices.Count > 0)
            {
                SelectDevice(_devices[0]);
            }

            CmbChartRange.SelectedIndex = 1;
            LoadChartHistory();

            UpdateKpis();
            LogEvent("INFO", $"Đã nạp {savedDevices.Count} bản tin gần nhất từ SQLite: {_historyManager.DatabasePath}");

            Activate();
            Topmost = true;
            Topmost = false;
            Focus();

            BtnConnect_Click(this, new RoutedEventArgs());
        }

        #endregion

        #region 2. KẾT NỐI & NGẮT KẾT NỐI MQTT BROKER

        private void UpdateConnectionUiState(bool isConnected, bool isConnecting = false)
        {
            if (isConnecting)
            {
                BtnConnect.Visibility = Visibility.Visible;
                BtnDisconnect.Visibility = Visibility.Collapsed;
                BtnConnect.IsEnabled = false;
                BtnConnect.Content = "Đang kết nối...";
                DotBrokerStatus.Fill = new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B));
                BrokerStatusBadge.Background = new SolidColorBrush(Color.FromRgb(0xFE, 0xF3, 0xC7));
            }
            else if (isConnected)
            {
                BtnConnect.Visibility = Visibility.Collapsed;
                BtnDisconnect.Visibility = Visibility.Visible;
                BtnDisconnect.IsEnabled = true;
                DotBrokerStatus.Fill = new SolidColorBrush(Color.FromRgb(0x16, 0xA3, 0x4A));
                BrokerStatusBadge.Background = new SolidColorBrush(Color.FromRgb(0xDC, 0xFC, 0xE7));
            }
            else
            {
                BtnConnect.Visibility = Visibility.Visible;
                BtnDisconnect.Visibility = Visibility.Collapsed;
                BtnConnect.IsEnabled = true;
                BtnConnect.Content = "Kết Nối";
                DotBrokerStatus.Fill = new SolidColorBrush(Color.FromRgb(0xDC, 0x26, 0x26));
                BrokerStatusBadge.Background = new SolidColorBrush(Color.FromRgb(0xFE, 0xE2, 0xE2));
            }

            UpdateCommandAvailability();
        }

        private async void BtnConnect_Click(object sender, RoutedEventArgs e)
        {
            var host = TxtHost.Text.Trim();
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

            UpdateConnectionUiState(isConnected: false, isConnecting: true);
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
                LogEvent("MQTT", $"Cấu hình: Broker={host}:{port}; Root={topicRoot}; TLS={(settings.UseTls ? "Bật" : "Tắt")}");
            }
            catch (OperationCanceledException)
            {
                UpdateConnectionUiState(isConnected: false, isConnecting: false);
                ShowError("Kết nối MQTT quá thời gian chờ 10 giây.");
            }
            catch (Exception ex)
            {
                AppLogger.Error("DASHBOARD_CONNECT_FAILED", ex);
                UpdateConnectionUiState(isConnected: false, isConnecting: false);
                ShowError($"Không thể kết nối MQTT Broker: {ex.Message}");
            }
        }

        private async void BtnDisconnect_Click(object sender, RoutedEventArgs e)
        {
            UpdateConnectionUiState(isConnected: false, isConnecting: true);
            try
            {
                await _mqttController.DisconnectAsync();
            }
            catch (Exception ex)
            {
                AppLogger.Error("DASHBOARD_DISCONNECT_FAILED", ex);
                ShowError($"Không thể ngắt kết nối an toàn: {ex.Message}");
            }
            finally
            {
                UpdateConnectionUiState(isConnected: false, isConnecting: false);
            }
        }

        private void OnConnectionStatusChanged(bool isConnected, string message)
        {
            Dispatcher.BeginInvoke(() =>
            {
                LblStatus.Text = message;
                bool isConnecting = message.Contains("Đang kết nối") || message.Contains("thử kết nối lại");
                UpdateConnectionUiState(isConnected, isConnecting);
                if (!isConnected)
                {
                    foreach (var device in _devices)
                    {
                        device.IsOnline = false;
                        device.HasWarning = false;
                    }
                    UpdateKpis();
                    if (_selectedDevice != null) RefreshDetailView(_selectedDevice);
                }
                LogEvent(isConnected ? "SUCCESS" : "MQTT", message);
            });
        }

        private void BtnToggleConfig_Click(object sender, RoutedEventArgs e)
        {
            DrawerConfig.Visibility = DrawerConfig.Visibility == Visibility.Visible
                ? Visibility.Collapsed
                : Visibility.Visible;
        }

        private async void BtnSaveAndReconnect_Click(object sender, RoutedEventArgs e)
        {
            DrawerConfig.Visibility = Visibility.Collapsed;
            if (_mqttController.IsConnected)
            {
                try
                {
                    await _mqttController.DisconnectAsync();
                }
                catch (Exception ex)
                {
                    AppLogger.Warning("RECONNECT_DISCONNECT_WARN", ex.Message);
                }
            }
            BtnConnect_Click(sender, e);
        }

        private void ChkTopmost_Click(object sender, RoutedEventArgs e)
        {
            Topmost = ChkTopmost.IsChecked == true;
            if (Topmost)
            {
                Activate();
                Focus();
            }
        }

        #endregion

        #region 3. TIẾP NHẬN BẢN TIN MQTT & CẢNH BÁO AN TOÀN

        private void OnTelemetryPersistRequested(TelemetryMessage msg)
        {
            if (_historyWriter.Enqueue(msg)) return;

            var dropped = _historyWriter.Stats.Dropped;
            if (dropped - _lastReportedDropCount < 100) return;

            _lastReportedDropCount = dropped;
            Dispatcher.BeginInvoke(() => LogEvent(
                "WARN",
                $"[LỊCH SỬ] Hàng đợi ghi SQLite quá tải, đã bỏ {dropped} bản tin để giữ giao diện mượt."));
        }

        private void OnTelemetryReceived(TelemetryMessage msg)
        {
            Dispatcher.BeginInvoke(() =>
            {
                if (_isClosing) return;
                var dev = _devices.FirstOrDefault(d => d.DeviceId == msg.DeviceId);
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

                dev.DeviceType = msg.DeviceType;
                dev.Location = msg.Location;
                dev.IsOnline = true;
                dev.LastSeen = DateTime.Now.ToString("T");
                dev.UpdateTelemetryData(msg.Data);

                bool isWarning = false;
                string warningDetail = string.Empty;
                string recommendation = string.Empty;

                if (msg.Data.TryGetValue("temperature", out var tObj) && MessageValidator.TryGetFiniteDouble(tObj, out double temp) && temp > 40.0)
                {
                    isWarning = true;
                    warningDetail = $"Quá nhiệt môi trường: {temp:F1}°C (Ngưỡng an toàn <= 40°C)";
                    recommendation = "Khuyến nghị: Kiểm tra điều hòa phòng máy chủ, kích hoạt hệ thống làm mát khẩn cấp.";
                }
                else if (msg.Data.TryGetValue("power_watt", out var pObj) && MessageValidator.TryGetFiniteDouble(pObj, out double pwr) && pwr > 3000.0)
                {
                    isWarning = true;
                    warningDetail = $"Quá tải điện năng: {pwr:F1}W (Ngưỡng an toàn <= 3000W)";
                    recommendation = "Khuyến nghị: Sa thải phụ tải không thiết yếu để tránh sập aptomat tổng.";
                }
                else if (msg.Data.TryGetValue("aqi", out var aqiObj) && MessageValidator.TryGetFiniteDouble(aqiObj, out double aqi) && aqi > 100.0)
                {
                    isWarning = true;
                    warningDetail = $"Chất lượng không khí kém: AQI {aqi:F0} (Ngưỡng an toàn <= 100)";
                    recommendation = "Khuyến nghị: Bật quạt hút khí tươi và hệ thống lọc bụi mịn HEPA ngay.";
                }
                else if (msg.Data.TryGetValue("tamper_alert", out var taObj) && taObj is bool bTa && bTa)
                {
                    isWarning = true;
                    warningDetail = "Cảnh báo an ninh: Phát hiện dấu hiệu cạy phá vỏ cảm biến cửa!";
                    recommendation = "Khuyến nghị: Cử nhân viên an ninh kiểm tra trực tiếp cửa ra vào.";
                }

                dev.HasWarning = isWarning;
                dev.WarningMessage = warningDetail;
                dev.Recommendation = recommendation;

                if (isWarning)
                {
                    LogEvent("WARN", $"🚨 [{dev.DisplayName}]: {warningDetail}");
                }
                else
                {
                    LogEvent("INFO", $"[{dev.DisplayName}]: {dev.LatestTelemetrySummary}");
                }

                UpdateKpis();
                TrackChartTelemetry(msg);

                if (_selectedDevice != null && _selectedDevice.DeviceId == dev.DeviceId)
                {
                    RefreshDetailView(dev);
                }

                _historyDirty = true;
            });
        }

        private void OnDeviceStatusReceived(DeviceStatusMessage statusMsg)
        {
            Dispatcher.BeginInvoke(() =>
            {
                if (_isClosing) return;
                var dev = _devices.FirstOrDefault(d => d.DeviceId == statusMsg.DeviceId);
                bool isOnline = string.Equals(statusMsg.Status, "online", StringComparison.OrdinalIgnoreCase);

                if (dev != null)
                {
                    dev.IsOnline = isOnline;
                    dev.LastSeen = DateTime.Now.ToString("T");
                    if (!isOnline)
                    {
                        dev.HasWarning = false;
                    }
                }

                UpdateKpis();
                LogEvent(isOnline ? "SUCCESS" : "ERROR", $"[STATUS LWT] {DeviceDisplayNameResolver.GetFriendlyName(statusMsg.DeviceId)}: {(isOnline ? "ONLINE" : "OFFLINE")}");

                if (_selectedDevice != null && _selectedDevice.DeviceId == statusMsg.DeviceId)
                {
                    RefreshDetailView(_selectedDevice);
                }
            });
        }

        private void OnMessageRejected(string reason)
        {
            Dispatcher.BeginInvoke(() => LogEvent("WARN", $"[LỌC TIN NHẮN] {reason}"));
        }

        private bool MatchesDeviceFilter(object item)
        {
            if (item is not DeviceItem device) return false;
            var query = TxtDeviceSearch?.Text.Trim() ?? string.Empty;
            var matchesText = string.IsNullOrEmpty(query) ||
                new[] { device.DisplayName, device.DeviceId, device.LocationFriendly, device.Location }
                    .Any(value => value.Contains(query, StringComparison.CurrentCultureIgnoreCase));
            var matchesStatus = (CmbDeviceStatus?.SelectedIndex ?? 0) switch
            {
                1 => device.IsOnline,
                2 => device.HasWarning,
                3 => !device.IsOnline,
                _ => true
            };
            return matchesText && matchesStatus;
        }

        private void RefreshDeviceFilter()
        {
            if (_deviceView == null || TxtDeviceResults == null) return;
            _deviceView.Refresh();
            TxtDeviceResults.Text = _deviceView.Count == 0
                ? "Không tìm thấy thiết bị. Hãy đổi từ khóa hoặc bộ lọc."
                : $"Hiển thị {_deviceView.Count} / {_devices.Count} thiết bị";
        }

        private void DeviceSearch_Changed(object sender, TextChangedEventArgs e) => RefreshDeviceFilter();
        private void DeviceFilter_Changed(object sender, SelectionChangedEventArgs e) => RefreshDeviceFilter();

        private void DeviceCard_KeyDown(object sender, KeyEventArgs e)
        {
            if ((e.Key == Key.Enter || e.Key == Key.Space) &&
                sender is FrameworkElement { DataContext: DeviceItem device })
            {
                SelectDevice(device);
                e.Handled = true;
            }
        }

        private void UpdateCommandAvailability()
        {
            if (QuickControls == null || AdvancedControls == null || BtnSendCmd == null) return;
            var enabled = _mqttController.IsConnected && _selectedDevice?.IsOnline == true && !_commandBusy;
            QuickControls.IsEnabled = enabled;
            AdvancedControls.IsEnabled = enabled;
            BtnSendCmd.IsEnabled = enabled;
            if (!_commandBusy && !enabled)
                LblCommandStatus.Text = !_mqttController.IsConnected
                    ? "Kết nối broker để bắt đầu điều khiển."
                    : "Thiết bị offline. Hãy kiểm tra trình giả lập.";
            else if (enabled && (LblCommandStatus.Text.StartsWith("Kết nối broker") ||
                                 LblCommandStatus.Text.StartsWith("Thiết bị offline")))
                LblCommandStatus.Text = "Sẵn sàng nhận lệnh";
        }

        private void UpdateKpis()
        {
            int total = _devices.Count;
            int online = _devices.Count(d => d.IsOnline);
            int warning = _devices.Count(d => d.HasWarning);
            int offline = _devices.Count(d => !d.IsOnline);

            TxtKpiTotal.Text = total.ToString();
            TxtKpiOnline.Text = online.ToString();
            TxtKpiWarning.Text = warning.ToString();
            TxtKpiOffline.Text = offline.ToString();
            RefreshDeviceFilter();
            UpdateCommandAvailability();
        }

        #endregion

        #region 4. LỰA CHỌN THIẾT BỊ & CẬP NHẬT GIAO DIỆN CHI TIẾT

        public void SelectDevice(DeviceItem dev)
        {
            if (dev == null) return;

            foreach (var d in _devices)
            {
                d.IsSelected = (d.DeviceId == dev.DeviceId);
            }

            _selectedDevice = dev;
            RefreshDetailView(dev);

            UpdatePresetCommandsForDevice(dev);
            UpdateCommandAvailability();
        }

        private void RefreshDetailView(DeviceItem dev)
        {
            TxtDetailDisplayName.Text = dev.DisplayName;
            TxtDetailCategory.Text = dev.CategoryName;
            TxtDetailIdAndLocation.Text = $"Vị trí: {dev.LocationFriendly} · Mã thiết bị: {dev.DeviceId}";
            TxtDetailStatus.Text = dev.StatusText;
            TxtDetailStatus.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(dev.StatusColor));
            BadgeDetailStatus.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(dev.StatusBadgeBg));
            TxtDetailLastSeen.Text = $"Cập nhật lần cuối: {dev.LastSeen}";

            if (TxtTechDeviceId != null) TxtTechDeviceId.Text = dev.DeviceId;
            var topicRoot = _mqttController.TopicRoot;
            if (TxtTechTopic != null) TxtTechTopic.Text = MqttTopics.Telemetry(topicRoot, dev.Location, dev.DeviceType, dev.DeviceId);
            if (TxtTechCmdTopic != null) TxtTechCmdTopic.Text = MqttTopics.Command(topicRoot, dev.Location, dev.DeviceType, dev.DeviceId);

            DetailDeviceVisual.Content = dev;
            var fadeIn = new System.Windows.Media.Animation.DoubleAnimation(0.3, 1.0, TimeSpan.FromMilliseconds(250));
            DetailDeviceVisual.BeginAnimation(UIElement.OpacityProperty, fadeIn);

            if (dev.HasWarning)
            {
                BannerWarning.Visibility = Visibility.Visible;
                TxtWarningMessage.Text = dev.WarningMessage;
                TxtWarningRecommendation.Text = dev.Recommendation;
            }
            else
            {
                BannerWarning.Visibility = Visibility.Collapsed;
            }

            if (!ReferenceEquals(IcDetailMetrics.ItemsSource, dev.DetailedMetrics))
                IcDetailMetrics.ItemsSource = dev.DetailedMetrics;

            TxtControlDeviceTitle.Text = "Điều khiển thiết bị";

            PanelCtrlLight.Visibility = (dev.DeviceId == "smart_light_01") ? Visibility.Visible : Visibility.Collapsed;
            PanelCtrlDoor.Visibility = (dev.DeviceId == "door_sensor_01") ? Visibility.Visible : Visibility.Collapsed;
            PanelCtrlMeter.Visibility = (dev.DeviceId == "power_meter_01") ? Visibility.Visible : Visibility.Collapsed;
            PanelCtrlAir.Visibility = (dev.DeviceId == "air_quality_01") ? Visibility.Visible : Visibility.Collapsed;
            PanelCtrlClimate.Visibility = (dev.DeviceId == "temp_hum_01") ? Visibility.Visible : Visibility.Collapsed;

            if (dev.DeviceId == "smart_light_01" && !SliderBrightness.IsMouseCaptureWithin)
            {
                SliderBrightness.Value = dev.Brightness;
                TxtBrightnessValue.Text = $"{(int)SliderBrightness.Value}%";
            }

            TxtRawJson.Text = dev.RawTelemetryData != null && dev.RawTelemetryData.Count > 0
                ? JsonConvert.SerializeObject(dev.RawTelemetryData, Formatting.Indented)
                : "{\n  \"message\": \"Chưa có dữ liệu thô\"\n}";
        }

        private void DeviceCard_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is DeviceItem dev)
            {
                SelectDevice(dev);
            }
        }

        private void SelectDeviceButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is DeviceItem dev)
            {
                SelectDevice(dev);
            }
        }

        private void DgDevices_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DgDevices.SelectedItem is DeviceItem selected)
            {
                SelectDevice(selected);
            }
        }

        private void DgDevices_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (DgDevices.SelectedItem is not DeviceItem device) return;

            var historyWindow = new TelemetryHistoryWindow(device.DeviceId, _historyManager) { Owner = this };
            historyWindow.ShowDialog();
            RefreshHistoryTable();
        }

        #endregion

        #region 5. ĐIỀU KHIỂN THEO NGỮ CẢNH (CONTEXT-AWARE CONTROL)

        private async void ExecuteDeviceCommand(string command, Dictionary<string, object>? parameters = null)
        {
            if (_selectedDevice == null)
            {
                ShowError("Vui lòng chọn một thiết bị để điều khiển.");
                return;
            }

            if (_commandBusy) return;
            if (!_mqttController.IsConnected || !_selectedDevice.IsOnline)
            {
                LblCommandStatus.Text = "Cần kết nối broker và chờ thiết bị online để gửi lệnh.";
                return;
            }
            _commandBusy = true;
            UpdateCommandAvailability();
            var targetDevice = _selectedDevice;
            parameters ??= new Dictionary<string, object>();
            LblCommandStatus.Text = "Đang gửi lệnh...";
            LblCommandStatus.Foreground = Brushes.DarkOrange;

            try
            {
                await _mqttController.SendCommandAsync(
                    targetDevice.Location,
                    targetDevice.DeviceType,
                    targetDevice.DeviceId,
                    command,
                    parameters);

                LogEvent("CMD", $"Đã gửi lệnh '{command}' tới {targetDevice.DisplayName} ({targetDevice.DeviceId})");
                LblCommandStatus.Text = $"Đã publish tới {targetDevice.DisplayName}: {command}. Theo dõi dữ liệu để xác nhận thay đổi.";
                LblCommandStatus.Foreground = Brushes.Green;
            }
            catch (Exception ex)
            {
                AppLogger.Error("COMMAND_EXECUTE_FAILED", ex);
                LblCommandStatus.Text = $"Lỗi: {ex.Message}";
                LblCommandStatus.Foreground = Brushes.Red;
                ShowError($"Không thể thực thi lệnh: {ex.Message}");
            }
            finally
            {
                _commandBusy = false;
                UpdateCommandAvailability();
            }
        }

        private void BtnLightOn_Click(object sender, RoutedEventArgs e) =>
            ExecuteDeviceCommand("TOGGLE_POWER", new Dictionary<string, object> { ["state"] = "ON" });

        private void BtnLightOff_Click(object sender, RoutedEventArgs e) =>
            ExecuteDeviceCommand("TOGGLE_POWER", new Dictionary<string, object> { ["state"] = "OFF" });

        private void SliderBrightness_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (TxtBrightnessValue != null)
            {
                TxtBrightnessValue.Text = $"{(int)e.NewValue}%";
            }
        }

        private void SliderBrightness_Commit(object sender, MouseButtonEventArgs e)
        {
            if (_selectedDevice?.DeviceId == "smart_light_01")
                ExecuteDeviceCommand("SET_BRIGHTNESS", new Dictionary<string, object> { ["brightness"] = (int)SliderBrightness.Value });
        }

        private void SliderBrightness_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key is Key.Left or Key.Right or Key.Up or Key.Down or Key.Home or Key.End or Key.PageUp or Key.PageDown)
            {
                if (_selectedDevice?.DeviceId == "smart_light_01")
                    ExecuteDeviceCommand("SET_BRIGHTNESS", new Dictionary<string, object> { ["brightness"] = (int)SliderBrightness.Value });
            }
        }

        private void BtnBrightnessPreset_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && int.TryParse(btn.Tag?.ToString(), out int val))
            {
                SliderBrightness.Value = val;
                ExecuteDeviceCommand("SET_BRIGHTNESS", new Dictionary<string, object> { ["brightness"] = val });
            }
        }

        private void BtnDoorOpen_Click(object sender, RoutedEventArgs e) =>
            ExecuteDeviceCommand("OPEN_DOOR");

        private void BtnDoorClose_Click(object sender, RoutedEventArgs e) =>
            ExecuteDeviceCommand("CLOSE_DOOR");

        private void BtnDoorTriggerAlarm_Click(object sender, RoutedEventArgs e) =>
            ExecuteDeviceCommand("TRIGGER_ALARM");

        private void BtnDoorClearAlarm_Click(object sender, RoutedEventArgs e) =>
            ExecuteDeviceCommand("CLEAR_ALARM");

        private void BtnMeterLoad500_Click(object sender, RoutedEventArgs e) =>
            ExecuteDeviceCommand("SET_LOAD", new Dictionary<string, object> { ["power_watt"] = 500.0 });

        private void BtnMeterLoad2000_Click(object sender, RoutedEventArgs e) =>
            ExecuteDeviceCommand("SET_LOAD", new Dictionary<string, object> { ["power_watt"] = 2000.0 });

        private void BtnMeterOverload_Click(object sender, RoutedEventArgs e) =>
            ExecuteDeviceCommand("TRIGGER_OVERLOAD");

        private void BtnMeterResetEnergy_Click(object sender, RoutedEventArgs e) =>
            ExecuteDeviceCommand("RESET_ENERGY");

        private void BtnAirPurify_Click(object sender, RoutedEventArgs e) =>
            ExecuteDeviceCommand("PURIFY_AIR");

        private void BtnAirAqi85_Click(object sender, RoutedEventArgs e) =>
            ExecuteDeviceCommand("SET_AQI", new Dictionary<string, object> { ["aqi"] = 85.0 });

        private void BtnAirAqi180_Click(object sender, RoutedEventArgs e) =>
            ExecuteDeviceCommand("SET_AQI", new Dictionary<string, object> { ["aqi"] = 180.0 });

        private void BtnAirTriggerAlarm_Click(object sender, RoutedEventArgs e) =>
            ExecuteDeviceCommand("TRIGGER_POLLUTION_ALERT");

        private void BtnClimateTemp22_Click(object sender, RoutedEventArgs e) =>
            ExecuteDeviceCommand("SET_TEMPERATURE", new Dictionary<string, object> { ["temperature"] = 22.0 });

        private void BtnClimateTemp28_Click(object sender, RoutedEventArgs e) =>
            ExecuteDeviceCommand("SET_TEMPERATURE", new Dictionary<string, object> { ["temperature"] = 28.0 });

        private void BtnClimateHeatAlert_Click(object sender, RoutedEventArgs e) =>
            ExecuteDeviceCommand("TRIGGER_HEAT_ALERT");

        private void BtnClimateCalibrate_Click(object sender, RoutedEventArgs e) =>
            ExecuteDeviceCommand("CALIBRATE");

        private void BtnSendCmd_Click(object sender, RoutedEventArgs e)
        {
            var command = TxtCommand.Text.Trim();
            if (string.IsNullOrWhiteSpace(command))
            {
                ShowError("Vui lòng nhập tên lệnh.");
                return;
            }
            try
            {
                var parameters = JsonConvert.DeserializeObject<Dictionary<string, object>>(TxtParams.Text.Trim())
                    ?? new Dictionary<string, object>();
                ExecuteDeviceCommand(command, parameters);
            }
            catch (JsonException)
            {
                ShowError("Tham số chưa đúng định dạng JSON. Ví dụ: {\"state\": \"ON\"}");
            }
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
                    break;

                case "door_sensor_01":
                    CmbPresetCommands.Items.Add(new PresetCommandItem("🚪 Mở cửa (OPEN)", "OPEN_DOOR", "{}"));
                    CmbPresetCommands.Items.Add(new PresetCommandItem("🚪 Đóng cửa (CLOSED)", "CLOSE_DOOR", "{}"));
                    CmbPresetCommands.Items.Add(new PresetCommandItem("🚨 Kích hoạt cảnh báo cạy phá", "TRIGGER_ALARM", "{}"));
                    CmbPresetCommands.Items.Add(new PresetCommandItem("🛡️ Hủy cảnh báo cạy phá", "CLEAR_ALARM", "{}"));
                    break;

                case "temp_hum_01":
                    CmbPresetCommands.Items.Add(new PresetCommandItem("❄️ Đặt nhiệt độ mát (22°C)", "SET_TEMPERATURE", "{\"temperature\": 22.0}"));
                    CmbPresetCommands.Items.Add(new PresetCommandItem("🌡️ Đặt nhiệt độ phòng (28°C)", "SET_TEMPERATURE", "{\"temperature\": 28.0}"));
                    CmbPresetCommands.Items.Add(new PresetCommandItem("🔥 Thử nghiệm quá nhiệt (52°C)", "SET_TEMPERATURE", "{\"temperature\": 52.0}"));
                    CmbPresetCommands.Items.Add(new PresetCommandItem("🚨 Kích hoạt cảnh báo nhiệt độ cao", "TRIGGER_HEAT_ALERT", "{}"));
                    CmbPresetCommands.Items.Add(new PresetCommandItem("⚖️ Hiệu chuẩn cảm biến", "CALIBRATE", "{}"));
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

        #endregion

        #region 6. WORKSPACE TABS: LỊCH SỬ, NHẬT KÝ & DỮ LIỆU THÔ

        private async void RefreshHistoryTable()
        {
            _historyDirty = true;
            var requestVersion = ++_historyRequestVersion;
            if (_historyLoading || _isClosing ||
                CmbHistoryFilter?.SelectedItem is not HistoryDeviceFilterOption filter) return;

            _historyLoading = true;
            _historyDirty = false;
            var deviceId = filter.DeviceId;
            try
            {
                var displayItems = await Task.Run(() => _historyManager.GetFilteredHistory(deviceId, 100)
                    .Select(TelemetryHistoryDisplayItem.FromMessage).ToList());
                if (_isClosing || requestVersion != _historyRequestVersion) return;
                DgHistory.ItemsSource = displayItems;
                if (TxtHistoryRowCount != null)
                    TxtHistoryRowCount.Text = $"Hiển thị: {displayItems.Count} bản tin";
            }
            catch (Exception ex)
            {
                AppLogger.Error("HISTORY_REFRESH_FAILED", ex);
            }
            finally { _historyLoading = false; }
        }

        private void TrackChartTelemetry(TelemetryMessage msg)
        {
            List<(DateTime Timestamp, double Value)>? buffer = null;

            if (msg.DeviceId == ChartDeviceIdTemp &&
                msg.Data.TryGetValue("temperature", out var tObj) &&
                MessageValidator.TryGetFiniteDouble(tObj, out double t))
            {
                buffer = _chartPointsTemp;
                buffer.Add((DateTime.Now, t));
            }
            else if (msg.DeviceId == ChartDeviceIdPower &&
                msg.Data.TryGetValue("power_watt", out var pObj) &&
                MessageValidator.TryGetFiniteDouble(pObj, out double p))
            {
                buffer = _chartPointsPower;
                buffer.Add((DateTime.Now, p));
            }
            else if (msg.DeviceId == ChartDeviceIdAqi &&
                msg.Data.TryGetValue("aqi", out var aObj) &&
                MessageValidator.TryGetFiniteDouble(aObj, out double a))
            {
                buffer = _chartPointsAqi;
                buffer.Add((DateTime.Now, a));
            }

            if (buffer == null) return;

            if (buffer.Count > ChartMaxBufferedPoints)
                buffer.RemoveRange(0, buffer.Count - ChartMaxBufferedPoints);

            _chartsDirty = true;
            if (MainTabs.SelectedItem == TabCharts) RenderCharts();
        }

        private void LoadChartHistory()
        {
            PopulateChartBuffer(_chartPointsTemp, ChartDeviceIdTemp, "temperature");
            PopulateChartBuffer(_chartPointsPower, ChartDeviceIdPower, "power_watt");
            PopulateChartBuffer(_chartPointsAqi, ChartDeviceIdAqi, "aqi");
            RenderCharts();
        }

        private void PopulateChartBuffer(List<(DateTime Timestamp, double Value)> buffer, string deviceId, string dataKey)
        {
            buffer.Clear();
            var history = _historyManager.GetFilteredHistory(deviceId, ChartMaxBufferedPoints);
            history.Reverse();

            foreach (var msg in history)
            {
                if (!msg.Data.TryGetValue(dataKey, out var raw) || !MessageValidator.TryGetFiniteDouble(raw, out double value))
                    continue;

                var timestamp = MessageValidator.TryParseUtcTimestamp(msg.Timestamp, out var parsed)
                    ? parsed.LocalDateTime
                    : DateTime.Now;

                buffer.Add((timestamp, value));
            }
        }

        private void RenderCharts()
        {
            _chartsDirty = false;

            ChartTemperature.SetSeries(
                "Nhiệt độ",
                "°C",
                new SolidColorBrush(Color.FromRgb(0x25, 0x63, 0xEB)),
                TakeLastPoints(_chartPointsTemp),
                ChartThresholdTemp,
                "Ngưỡng 40°C");

            ChartPower.SetSeries(
                "Công suất",
                "W",
                new SolidColorBrush(Color.FromRgb(0x08, 0x91, 0xB2)),
                TakeLastPoints(_chartPointsPower),
                ChartThresholdPower,
                "Ngưỡng 3000W");

            ChartAqi.SetSeries(
                "Chỉ số AQI",
                "AQI",
                new SolidColorBrush(Color.FromRgb(0xEA, 0x58, 0x0C)),
                TakeLastPoints(_chartPointsAqi),
                ChartThresholdAqi,
                "Ngưỡng 100");
        }

        private List<(DateTime Timestamp, double Value)> TakeLastPoints(List<(DateTime Timestamp, double Value)> source)
        {
            return source.Count <= _chartPointLimit
                ? new List<(DateTime, double)>(source)
                : source.GetRange(source.Count - _chartPointLimit, _chartPointLimit);
        }

        private void CmbChartRange_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CmbChartRange.SelectedItem is ComboBoxItem item && int.TryParse((string)item.Tag, out int limit))
            {
                _chartPointLimit = limit;
                if (ChartTemperature != null) RenderCharts();
            }
        }

        private void BtnRefreshCharts_Click(object sender, RoutedEventArgs e)
        {
            LoadChartHistory();
        }

        private void BtnViewSelectedHistory_Click(object sender, RoutedEventArgs e)
        {
            MainTabs.SelectedItem = TabHistory;
            if (_selectedDevice != null && CmbHistoryFilter.ItemsSource is List<HistoryDeviceFilterOption> options)
            {
                var match = options.FirstOrDefault(o => o.DeviceId == _selectedDevice.DeviceId);
                if (match != null)
                {
                    CmbHistoryFilter.SelectedItem = match;
                }
            }
            RefreshHistoryTable();
        }

        private void CmbHistoryFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            RefreshHistoryTable();
            if (PanelHistoryDetail != null)
            {
                PanelHistoryDetail.Visibility = Visibility.Collapsed;
            }
        }

        private void BtnRefreshHistory_Click(object sender, RoutedEventArgs e)
        {
            RefreshHistoryTable();
        }

        private void BtnClearSelectedHistory_Click(object sender, RoutedEventArgs e)
        {
            if (CmbHistoryFilter.SelectedItem is not HistoryDeviceFilterOption filter) return;

            if (string.IsNullOrEmpty(filter.DeviceId))
            {
                var confirm = MessageBox.Show(this,
                    "Bạn có chắc chắn muốn xóa toàn bộ lịch sử đo lường của TẤT CẢ các thiết bị?",
                    "Xác nhận xóa", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (confirm == MessageBoxResult.Yes)
                {
                    _historyManager.ClearAll();
                    RefreshHistoryTable();
                    if (PanelHistoryDetail != null) PanelHistoryDetail.Visibility = Visibility.Collapsed;
                    LogEvent("INFO", "Đã xóa toàn bộ lịch sử SQLite của tất cả thiết bị.");
                }
            }
            else
            {
                var friendly = DeviceDisplayNameResolver.GetFriendlyName(filter.DeviceId);
                var confirm = MessageBox.Show(this,
                    $"Bạn có chắc chắn muốn xóa toàn bộ lịch sử đo lường của '{friendly}' ({filter.DeviceId})?",
                    "Xác nhận xóa", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (confirm == MessageBoxResult.Yes)
                {
                    _historyManager.ClearHistory(filter.DeviceId);
                    RefreshHistoryTable();
                    if (PanelHistoryDetail != null) PanelHistoryDetail.Visibility = Visibility.Collapsed;
                    LogEvent("INFO", $"Đã xóa lịch sử SQLite của {friendly}.");
                }
            }
        }

        private void DgHistory_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DgHistory.SelectedItem is TelemetryHistoryDisplayItem item)
            {
                PanelHistoryDetail.Visibility = Visibility.Visible;
                TxtHistoryDetailTitle.Text = $"{item.DisplayName} ({item.DeviceId}) · Nhận lúc: {item.TimestampFormatted}";
                TxtHistoryDetailJson.Text = item.RawJsonFormatted;
            }
            else
            {
                PanelHistoryDetail.Visibility = Visibility.Collapsed;
            }
        }

        private void BtnCopyHistoryJson_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(TxtHistoryDetailJson.Text))
            {
                Clipboard.SetText(TxtHistoryDetailJson.Text);
                MessageBox.Show(this, "Đã sao chép gói tin JSON vào bộ nhớ tạm (Clipboard).", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BtnCloseHistoryDetail_Click(object sender, RoutedEventArgs e)
        {
            PanelHistoryDetail.Visibility = Visibility.Collapsed;
            DgHistory.SelectedItem = null;
        }

        private void LogEvent(string category, string message)
        {
            Dispatcher.BeginInvoke(() =>
            {
                _logEntries.Add(new LogEntry(category, message));
                if (_logEntries.Count > MaxLogLines)
                {
                    _logEntries.RemoveAt(0);
                }

                if (ChkAutoScrollLog?.IsChecked == true && LbLogEntries.Items.Count > 0)
                {
                    LbLogEntries.ScrollIntoView(LbLogEntries.Items[^1]);
                }
            });

            AppLogger.Info("DASHBOARD_EVENT", $"[{category}] {message}");
        }

        private void BtnClearLog_Click(object sender, RoutedEventArgs e)
        {
            _logEntries.Clear();
            LogEvent("INFO", "Đã dọn dẹp bộ nhớ nhật ký sự kiện.");
        }

        private void BtnCopyRawJson_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(TxtRawJson.Text))
            {
                Clipboard.SetText(TxtRawJson.Text);
                MessageBox.Show(this, "Đã sao chép gói tin JSON thô vào bộ nhớ tạm (Clipboard).", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private async void MainWindow_Closing(object? sender, CancelEventArgs e)
        {
            if (_allowClose) return;

            e.Cancel = true;
            if (_isClosing) return;
            _isClosing = true;
            _historyRefreshTimer.Stop();
            IsEnabled = false;
            LblStatus.Text = "Đang ngắt kết nối và đóng ứng dụng...";

            try
            {
                await _mqttController.DisposeAsync();

                await _historyWriter.DisposeAsync();
            }
            catch (Exception ex)
            {
                AppLogger.Error("DASHBOARD_SHUTDOWN_FAILED", ex);
            }
            finally
            {

                _ = Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() =>
                {
                    _allowClose = true;
                    Close();
                }));
            }
        }

        private void ShowError(string message)
        {
            MessageBox.Show(this, message, "Thông báo lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        #endregion
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

    public class HistoryDeviceFilterOption
    {
        public string DisplayText { get; set; } = string.Empty;
        public string? DeviceId { get; set; }

        public override string ToString() => DisplayText;
    }
}
