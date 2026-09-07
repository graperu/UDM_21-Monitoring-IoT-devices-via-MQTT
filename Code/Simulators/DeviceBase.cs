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
        private readonly MessageDeduplicator _processedCommandIds = new MessageDeduplicator(100);

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

            Mqtt = new MqttHelper($"sim_{DeviceId}_{Guid.NewGuid().ToString("N").Substring(0, 4)}");
            Mqtt.MessageReceivedAsync += OnMessageReceivedAsync;

            // Moi khi ket noi (bao gom ca lan dau va cac lan RECONNECT sau khi rot mang),
            // tu dong publish lai trang thai "online" (retained) de Dashboard luon hien thi dung.
            Mqtt.ConnectionChangedAsync += OnMqttConnectionChangedAsync;
        }

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

            var lwtStatus = new DeviceStatusMessage 
            { 
                MessageId = Guid.NewGuid().ToString(),
                DeviceId = DeviceId, 
                Status = "offline",
                Timestamp = DateTime.UtcNow.ToString("o")
            };
            
            try
            {
                // Đăng ký trước để subscription được ghi nhớ cả khi lần kết nối đầu thất bại.
                await Mqtt.SubscribeAsync(CmdTopic);
                await Mqtt.ConnectAsync(settings, StatusTopic, lwtStatus.ToJson());
                Console.WriteLine($"[Device {DeviceId}] Online and active.");
                AppLogger.Info("DEVICE_STARTED", $"device={DeviceId}; endpoint={settings.Host}:{settings.Port}; tls={settings.UseTls}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Device {DeviceId}] Unreachable broker '{settings.Host}:{settings.Port}' ({ex.Message}). Auto-reconnect active...");
                AppLogger.Error("DEVICE_CONNECT_FAILED", ex);
            }

            // Start telemetry publish loop regardless, auto-reconnect will re-establish session when broker is ready
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
                catch (OperationCanceledException)
                {
                    // Kết thúc bình thường khi thiết bị dừng.
                }
            }

            var offlineStatus = new DeviceStatusMessage 
            { 
                MessageId = Guid.NewGuid().ToString(),
                DeviceId = DeviceId, 
                Status = "offline",
                Timestamp = DateTime.UtcNow.ToString("o")
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

        private async Task TelemetryLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    var data = GenerateTelemetry();
                    var msg = new TelemetryMessage
                    {
                        MessageId = Guid.NewGuid().ToString(),
                        DeviceId = DeviceId,
                        DeviceType = DeviceType,
                        Location = Location,
                        Timestamp = DateTime.UtcNow.ToString("o"),
                        Data = data
                    };

                    await Mqtt.PublishAsync(TelemetryTopic, msg.ToJson(), MQTTnet.Protocol.MqttQualityOfServiceLevel.AtMostOnce);
                }
                catch (Exception ex)
                {
                    // Neu dang mat ket noi, PublishAsync co the nem loi - bo qua chu ky nay,
                    // vong lap TelemetryLoopAsync van tiep tuc chay o chu ky ke tiep.
                    // MqttHelper se tu lo viec ket noi lai o cho khac.
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

        protected abstract Dictionary<string, object> GenerateTelemetry();

        protected virtual Task HandleCommandAsync(CommandMessage cmd)
        {
            Console.WriteLine($"[Device {DeviceId}] Received command: {cmd.Command}");
            return Task.CompletedTask;
        }

        private Task OnMessageReceivedAsync(string topic, string payload, MQTTnet.Protocol.MqttQualityOfServiceLevel qos, bool retain)
        {
            if (topic != CmdTopic) return Task.CompletedTask;

            if (!MessageValidator.TryParseCommand(payload, out var cmd, out var error) || cmd == null)
            {
                AppLogger.Warning("COMMAND_REJECTED", $"device={DeviceId}; reason={error}");
                return Task.CompletedTask;
            }

            if (!MessageValidator.TryValidateCommandForDevice(DeviceType, cmd.Command, cmd.Params, out error))
            {
                AppLogger.Warning("COMMAND_REJECTED", $"device={DeviceId}; reason={error}");
                return Task.CompletedTask;
            }

            if (!_processedCommandIds.TryAccept(cmd.MessageId))
            {
                AppLogger.Warning("COMMAND_DUPLICATE", $"device={DeviceId}; message_id={cmd.MessageId}");
                return Task.CompletedTask;
            }

            cmd.Command = cmd.Command.Trim().ToUpperInvariant();
            AppLogger.Info("COMMAND_RECEIVED", $"device={DeviceId}; command={cmd.Command}");
            return HandleCommandAsync(cmd);
        }

        // Duoc goi moi khi trang thai ket noi MQTT thay doi (ket noi lan dau, mat ket noi, hoac reconnect thanh cong)
        private async Task OnMqttConnectionChangedAsync(bool isConnected)
        {
            if (isConnected)
            {
                var onlineStatus = new DeviceStatusMessage 
                { 
                    MessageId = Guid.NewGuid().ToString(),
                    DeviceId = DeviceId, 
                    Status = "online",
                    Timestamp = DateTime.UtcNow.ToString("o")
                };
                await Mqtt.PublishAsync(StatusTopic, onlineStatus.ToJson(), MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce, retain: true);
                Console.WriteLine($"[Device {DeviceId}] (Re)connected to Broker - published online status.");
            }
            else
            {
                Console.WriteLine($"[Device {DeviceId}] Disconnected from Broker.");
            }
        }

        public async ValueTask DisposeAsync()
        {
            await StopAsync();
            await Mqtt.DisposeAsync();
        }
    }
}
