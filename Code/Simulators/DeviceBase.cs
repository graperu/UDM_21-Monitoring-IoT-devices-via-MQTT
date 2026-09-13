using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UDM_21.Shared;

namespace UDM_21.Simulators
{
    public abstract class DeviceBase : IAsyncDisposable
    {
        public string DeviceId { get; }
        public string DeviceType { get; }
        public string Location { get; }
        public int PublishIntervalSeconds { get; }
        public string TopicRoot { get; }

        protected string TelemetryTopic => MqttTopics.Telemetry(TopicRoot, Location, DeviceType, DeviceId);
        protected string StatusTopic => MqttTopics.Status(TopicRoot, Location, DeviceType, DeviceId);
        protected string CmdTopic => MqttTopics.Command(TopicRoot, Location, DeviceType, DeviceId);

        protected readonly MqttHelper Mqtt;
        private CancellationTokenSource? _cts;
        private Task? _telemetryTask;
        private bool _stopped;
        private readonly MessageDeduplicator _processedCommandIds = new(100);

        #region 1. KHỞI TẠO THIẾT BỊ

        protected DeviceBase(
            string deviceId,
            string deviceType,
            string location,
            int publishIntervalSeconds = 3,
            string topicRoot = MqttTopics.DefaultRoot)
        {
            DeviceId = deviceId;
            DeviceType = deviceType;
            Location = location;
            PublishIntervalSeconds = publishIntervalSeconds;
            TopicRoot = MqttTopics.NormalizeRoot(topicRoot);

            Mqtt = new MqttHelper($"sim_{DeviceId}_{Guid.NewGuid():N}"[..12]);
            Mqtt.MessageReceivedAsync += OnMessageReceivedAsync;
            Mqtt.ConnectionChangedAsync += OnMqttConnectionChangedAsync;
        }

        #endregion

        #region 2. KẾT NỐI BROKER & LAST WILL AND TESTAMENT (LWT)

        public async Task StartAsync(string brokerHost = "broker.emqx.io", int brokerPort = 1883)
        {
            await StartAsync(new MqttConnectionSettings { Host = brokerHost, Port = brokerPort });
        }

        public async Task StartAsync(MqttConnectionSettings settings)
        {
            if (_cts != null) throw new InvalidOperationException($"Device {DeviceId} đã được khởi động.");
            ArgumentNullException.ThrowIfNull(settings);
            settings.Validate();
            _cts = new CancellationTokenSource();
            _stopped = false;

            // Cấu hình LWT: Khi thiết bị mất kết nối đột ngột, Broker tự động phát bản tin "offline"
            var lwtStatus = new DeviceStatusMessage
            {
                DeviceId = DeviceId,
                Status = "offline"
            };

            try
            {
                await Mqtt.SubscribeAsync(CmdTopic);
                await Mqtt.ConnectAsync(settings, StatusTopic, lwtStatus.ToJson());
                Console.WriteLine($"[Device {DeviceId}] Online and active.");
                AppLogger.Info("DEVICE_STARTED", $"device={DeviceId}; endpoint={settings.Host}:{settings.Port}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Device {DeviceId}] Broker unreachable ({ex.Message}). Auto-reconnect active...");
                AppLogger.Error("DEVICE_CONNECT_FAILED", ex);
            }

            // Bắt đầu vòng lặp phát Telemetry trên nền bất đồng bộ
            _telemetryTask = Task.Run(() => TelemetryLoopAsync(_cts.Token));
        }

        public async Task StopAsync()
        {
            if (_stopped) return;
            _stopped = true;
            _cts?.Cancel();

            if (_telemetryTask != null)
            {
                try
                {
                    await _telemetryTask;
                }
                catch (OperationCanceledException) { }
            }

            // Gửi bản tin Offline chủ động khi người dùng dừng thiết bị bình thường
            var offlineStatus = new DeviceStatusMessage
            {
                DeviceId = DeviceId,
                Status = "offline"
            };

            if (Mqtt.IsConnected)
            {
                try
                {
                    await Mqtt.PublishAsync(StatusTopic, offlineStatus.ToJson(), MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce, retain: true);
                }
                catch (Exception ex)
                {
                    AppLogger.Error("DEVICE_OFFLINE_STATUS_FAILED", ex);
                }
            }

            await Mqtt.DisconnectAsync();
            _cts?.Dispose();
            _cts = null;
            Console.WriteLine($"[Device {DeviceId}] Offline and stopped.");
            AppLogger.Info("DEVICE_STOPPED", $"device={DeviceId}");
        }

        private async Task OnMqttConnectionChangedAsync(bool isConnected)
        {
            if (isConnected)
            {
                // Mỗi khi kết nối thành công, cập nhật trạng thái Online (retained = true)
                var onlineStatus = new DeviceStatusMessage
                {
                    DeviceId = DeviceId,
                    Status = "online"
                };
                await Mqtt.PublishAsync(StatusTopic, onlineStatus.ToJson(), MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce, retain: true);
                Console.WriteLine($"[Device {DeviceId}] (Re)connected to Broker - published online status.");
            }
            else
            {
                Console.WriteLine($"[Device {DeviceId}] Disconnected from Broker.");
            }
        }

        #endregion

        #region 3. VÒNG LẶP PHÁT DỮ LIỆU TELEMETRY

        protected abstract Dictionary<string, object> GenerateTelemetry();

        protected async Task PublishTelemetryAsync(MQTTnet.Protocol.MqttQualityOfServiceLevel qos = MQTTnet.Protocol.MqttQualityOfServiceLevel.AtMostOnce)
        {
            var msg = new TelemetryMessage
            {
                DeviceId = DeviceId,
                DeviceType = DeviceType,
                Location = Location,
                Data = GenerateTelemetry()
            };

            await Mqtt.PublishAsync(TelemetryTopic, msg.ToJson(), qos);
        }

        private async Task TelemetryLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await PublishTelemetryAsync(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtMostOnce);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Device {DeviceId}] Telemetry error: {ex.Message}");
                }

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(PublishIntervalSeconds), token);
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    break;
                }
            }
        }

        #endregion

        #region 4. TIẾP NHẬN & PHẢN HỒI LỆNH ĐIỀU KHIỂN (COMMAND / ACK)

        protected virtual Task HandleCommandAsync(CommandMessage cmd)
        {
            Console.WriteLine($"[Device {DeviceId}] Received command: {cmd.Command}");
            return Task.CompletedTask;
        }

        private Task OnMessageReceivedAsync(string topic, string payload, MQTTnet.Protocol.MqttQualityOfServiceLevel qos, bool retain)
        {
            if (topic != CmdTopic) return Task.CompletedTask;

            // 1. Kiểm tra cú pháp JSON của lệnh
            if (!MessageValidator.TryParseCommand(payload, out var cmd, out var error) || cmd == null)
            {
                AppLogger.Warning("COMMAND_REJECTED", $"device={DeviceId}; reason={error}");
                return Task.CompletedTask;
            }

            // 2. Kiểm tra lệnh có được hỗ trợ cho loại thiết bị này không
            if (!MessageValidator.TryValidateCommandForDevice(DeviceType, cmd.Command, cmd.Params, out error))
            {
                AppLogger.Warning("COMMAND_REJECTED", $"device={DeviceId}; reason={error}");
                return Task.CompletedTask;
            }

            // 3. Chống lặp lệnh trùng ID
            if (!_processedCommandIds.TryAccept(cmd.MessageId))
            {
                AppLogger.Warning("COMMAND_DUPLICATE", $"device={DeviceId}; message_id={cmd.MessageId}");
                return Task.CompletedTask;
            }

            cmd.Command = cmd.Command.Trim().ToUpperInvariant();
            AppLogger.Info("COMMAND_RECEIVED", $"device={DeviceId}; command={cmd.Command}");
            return HandleCommandAsync(cmd);
        }

        #endregion

        public async ValueTask DisposeAsync()
        {
            await StopAsync();
            await Mqtt.DisposeAsync();
        }
    }
}
