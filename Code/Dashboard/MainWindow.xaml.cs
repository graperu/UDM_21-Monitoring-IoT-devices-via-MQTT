using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using Newtonsoft.Json;
using UDM_21.Dashboard.Controllers;
using UDM_21.Dashboard.Models;

namespace UDM_21.Dashboard
{
    public partial class MainWindow : Window
    {
        private readonly MqttController _mqttController;
        private readonly ObservableCollection<DeviceItem> _devices = new ObservableCollection<DeviceItem>();

        public MainWindow()
        {
            InitializeComponent();
            _mqttController = new MqttController();

            DgDevices.ItemsSource = _devices;
            CmbDevices.ItemsSource = _devices;

            _mqttController.ConnectionStatusChanged += OnConnectionStatusChanged;
            _mqttController.TelemetryReceived += OnTelemetryReceived;
            _mqttController.DeviceStatusReceived += OnDeviceStatusReceived;
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
                MessageBox.Show("Port không hợp lệ!", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
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

                dev.IsOnline = true;
                dev.LastSeen = DateTime.Now.ToString("T");
                dev.RawTelemetryData = msg.Data;
                dev.LatestTelemetrySummary = JsonConvert.SerializeObject(msg.Data);

                LogConsole($"[TELEMETRY] {msg.DeviceId}: {dev.LatestTelemetrySummary}");
            });
        }

        private void OnDeviceStatusReceived(Shared.DeviceStatusMessage statusMsg)
        {
            Dispatcher.Invoke(() =>
            {
                var dev = _devices.FirstOrDefault(d => d.DeviceId == statusMsg.DeviceId);
                if (dev == null)
                {
                    dev = new DeviceItem
                    {
                        DeviceId = statusMsg.DeviceId,
                        IsOnline = statusMsg.Status.ToLower() == "online"
                    };
                    _devices.Add(dev);
                }
                else
                {
                    dev.IsOnline = statusMsg.Status.ToLower() == "online";
                }

                dev.LastSeen = DateTime.Now.ToString("T");
                LogConsole($"[STATUS] Device {statusMsg.DeviceId} is {statusMsg.Status.ToUpper()}");
            });
        }

        private async void BtnSendCmd_Click(object sender, RoutedEventArgs e)
        {
            if (CmbDevices.SelectedItem is not DeviceItem selectedDev)
            {
                MessageBox.Show("Vui lòng chọn thiết bị!", "Cảnh báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string cmd = TxtCommand.Text.Trim();
            try
            {
                var paramsDict = JsonConvert.DeserializeObject<System.Collections.Generic.Dictionary<string, object>>(TxtParams.Text.Trim())
                                 ?? new System.Collections.Generic.Dictionary<string, object>();

                await _mqttController.SendCommandAsync(selectedDev.Location, selectedDev.DeviceType, selectedDev.DeviceId, cmd, paramsDict);
                LogConsole($"[COMMAND SENT] To {selectedDev.DeviceId}: {cmd}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Cú pháp JSON không hợp lệ: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LogConsole(string message)
        {
            TxtConsole.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}\n");
            TxtConsole.ScrollToEnd();
        }
    }
}
