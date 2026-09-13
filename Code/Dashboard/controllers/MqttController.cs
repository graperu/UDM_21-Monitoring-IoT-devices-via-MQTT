using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MQTTnet.Protocol;
using UDM_21.Shared;

namespace UDM_21.Dashboard.Controllers
{
    public sealed class MqttController : IAsyncDisposable
    {
        private readonly MqttHelper _mqtt;
        private readonly TelemetryMessageFilter _telemetryFilter = new(100);
        private readonly MessageDeduplicator _statusDeduplicator = new(100);
        private readonly TimestampOrderingFilter _statusOrderingFilter = new();
        private string _topicRoot = MqttTopics.DefaultRoot;

        // Sự kiện gửi lên giao diện WPF
        public event Action<bool, string>? ConnectionStatusChanged;
        public event Action<TelemetryMessage>? TelemetryReceived;
        public event Action<DeviceStatusMessage>? DeviceStatusReceived;
        public event Action<string>? MessageRejected;

        public bool IsConnected => _mqtt.IsConnected;

        #region 1. KHỞI TẠO & KẾT NỐI MQTT BROKER

        public MqttController(string? clientId = null)
        {
            var uniqueId = clientId ?? $"WpfDashboard_{Guid.NewGuid():N}"[..19];
            _mqtt = new MqttHelper(uniqueId, cleanSession: true);
            _mqtt.ConnectionChangedAsync += OnConnectionChangedAsync;
            _mqtt.ReconnectingAsync += OnReconnectingAsync;
            _mqtt.MessageReceivedAsync += OnMessageReceivedAsync;
        }

        public async Task ConnectAsync(string host = "broker.emqx.io", int port = 1883)
        {
            await ConnectAsync(new MqttConnectionSettings { Host = host, Port = port }, MqttTopics.DefaultRoot);
        }

        public async Task ConnectAsync(MqttConnectionSettings settings, string topicRoot)
        {
            ArgumentNullException.ThrowIfNull(settings);
            settings.Validate();

            // Nếu đang kết nối, chủ động ngắt kết nối cũ an toàn trước khi kết nối lại
            if (_mqtt.IsConnected)
            {
                await _mqtt.DisconnectAsync();
                await Task.Delay(150);
            }

            _topicRoot = MqttTopics.NormalizeRoot(topicRoot);

            ConnectionStatusChanged?.Invoke(false, "Đang kết nối MQTT Broker...");
            _mqtt.ClearRememberedSubscriptions();
            await SubscribeWildcardTopicsAsync();
            await _mqtt.ConnectAsync(settings);
        }

        public async Task DisconnectAsync()
        {
            await _mqtt.DisconnectAsync();
            ConnectionStatusChanged?.Invoke(false, "Đã ngắt kết nối MQTT Broker.");
        }

        public async Task SubscribeWildcardTopicsAsync()
        {
            // Subscribe toàn bộ telemetry và status của tất cả thiết bị
            await _mqtt.SubscribeAsync(MqttTopics.TelemetryWildcard(_topicRoot), MqttQualityOfServiceLevel.AtLeastOnce);
            await _mqtt.SubscribeAsync(MqttTopics.StatusWildcard(_topicRoot), MqttQualityOfServiceLevel.AtLeastOnce);
        }

        #endregion

        #region 2. ĐIỀU PHỐI TIN NHẮN THEO TOPIC (ROUTING)

        private Task OnMessageReceivedAsync(string topic, string payload, MqttQualityOfServiceLevel qos, bool retain)
        {
            if (topic.EndsWith("/telemetry", StringComparison.Ordinal))
            {
                HandleTelemetry(topic, payload);
            }
            else if (topic.EndsWith("/status", StringComparison.Ordinal))
            {
                HandleStatus(topic, payload);
            }
            else
            {
                Reject($"Topic không được hỗ trợ: {topic}");
            }

            return Task.CompletedTask;
        }

        #endregion

        #region 3. BỘ LỌC CHỐNG TRÙNG & THỨ TỰ (DEDUPLICATION & ORDERING)

        private void HandleTelemetry(string topic, string payload)
        {
            // 1. Kiểm tra định dạng JSON
            if (!MessageValidator.TryParseTelemetry(payload, out var message, out var error) || message == null)
            {
                Reject(error);
                return;
            }

            // 2. Kiểm tra tính hợp lệ Topic
            if (!MessageValidator.ValidateTopic(topic, "telemetry", message.DeviceId, message.DeviceType, message.Location, out error, _topicRoot))
            {
                Reject(error);
                return;
            }

            // 3. Lọc trùng ID và lọc gói tin đến trễ theo Timestamp
            if (!_telemetryFilter.TryAccept(message, out error))
            {
                Reject(error);
                return;
            }

            // 4. Phát sự kiện cập nhật giao diện
            TelemetryReceived?.Invoke(message);
        }

        private void HandleStatus(string topic, string payload)
        {
            // 1. Kiểm tra định dạng JSON trạng thái (Online/Offline)
            if (!MessageValidator.TryParseStatus(payload, out var status, out var error) || status == null)
            {
                Reject(error);
                return;
            }

            // 2. Kiểm tra tính hợp lệ Topic
            if (!MessageValidator.ValidateTopic(topic, "status", status.DeviceId, null, null, out error, _topicRoot))
            {
                Reject(error);
                return;
            }

            // 3. Chống trùng bản tin status
            if (!_statusDeduplicator.TryAccept(status.MessageId))
            {
                Reject($"Duplicate status message: {status.MessageId}");
                return;
            }

            // 4. Kiểm tra thứ tự thời gian của thiết bị
            if (!_statusOrderingFilter.TryAccept(status.DeviceId, status.Timestamp, out error))
            {
                Reject(error);
                return;
            }

            // 5. Phát sự kiện trạng thái thiết bị
            DeviceStatusReceived?.Invoke(status);
        }

        #endregion

        #region 4. GỬI LỆNH ĐIỀU KHIỂN XUỐNG THIẾT BỊ (COMMAND)

        public async Task SendCommandAsync(
            string location,
            string deviceType,
            string deviceId,
            string command,
            Dictionary<string, object> parameters)
        {
            if (!MessageValidator.IsValidIdentifier(location) ||
                !MessageValidator.IsValidIdentifier(deviceType) ||
                !MessageValidator.IsValidIdentifier(deviceId))
            {
                throw new ArgumentException("Location, device type hoặc device ID không hợp lệ.");
            }

            parameters ??= new Dictionary<string, object>();
            command = command?.Trim().ToUpperInvariant() ?? string.Empty;

            if (!MessageValidator.TryValidateCommandForDevice(deviceType, command, parameters, out var error))
            {
                throw new ArgumentException(error, nameof(command));
            }

            var topic = MqttTopics.Command(_topicRoot, location, deviceType, deviceId);
            var commandMessage = new CommandMessage
            {
                Command = command,
                Params = parameters
            };

            await _mqtt.PublishAsync(topic, commandMessage.ToJson(), MqttQualityOfServiceLevel.AtLeastOnce);
            AppLogger.Info("COMMAND_SENT", $"device={deviceId}; command={command}");
        }

        #endregion

        #region 5. TRẠNG THÁI MẠNG & GIẢI PHÓNG TÀI NGUYÊN

        private Task OnConnectionChangedAsync(bool isConnected)
        {
            var message = isConnected
                ? "Đã kết nối thành công!"
                : _mqtt.IsManualDisconnect
                    ? "Đã ngắt kết nối MQTT Broker."
                    : "Mất kết nối với MQTT Broker! Đang thử kết nối lại...";

            ConnectionStatusChanged?.Invoke(isConnected, message);
            AppLogger.Info(isConnected ? "MQTT_CONNECTED" : "MQTT_DISCONNECTED", message);
            return Task.CompletedTask;
        }

        private Task OnReconnectingAsync(int attempt)
        {
            var message = $"Mất kết nối - đang thử kết nối lại (lần {attempt})...";
            ConnectionStatusChanged?.Invoke(false, message);
            AppLogger.Warning("MQTT_RECONNECTING", $"attempt={attempt}");
            return Task.CompletedTask;
        }

        private void Reject(string reason)
        {
            MessageRejected?.Invoke(reason);
            AppLogger.Warning("MESSAGE_REJECTED", reason);
        }

        public async ValueTask DisposeAsync()
        {
            _mqtt.ConnectionChangedAsync -= OnConnectionChangedAsync;
            _mqtt.ReconnectingAsync -= OnReconnectingAsync;
            _mqtt.MessageReceivedAsync -= OnMessageReceivedAsync;
            await _mqtt.DisposeAsync();
        }

        #endregion
    }
}
