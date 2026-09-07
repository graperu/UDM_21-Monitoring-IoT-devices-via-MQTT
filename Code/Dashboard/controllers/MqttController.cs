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
        private readonly TelemetryMessageFilter _telemetryFilter = new TelemetryMessageFilter(100);
        private readonly MessageDeduplicator _statusDeduplicator = new MessageDeduplicator(100);
        private readonly TimestampOrderingFilter _statusOrderingFilter = new TimestampOrderingFilter();
        private string _topicRoot = MqttTopics.DefaultRoot;

        public event Action<bool, string>? ConnectionStatusChanged;
        public event Action<TelemetryMessage>? TelemetryReceived;
        public event Action<DeviceStatusMessage>? DeviceStatusReceived;
        public event Action<string>? MessageRejected;

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
            await ConnectAsync(
                new MqttConnectionSettings { Host = host, Port = port },
                MqttTopics.DefaultRoot);
        }

        public async Task ConnectAsync(MqttConnectionSettings settings, string topicRoot)
        {
            ArgumentNullException.ThrowIfNull(settings);
            settings.Validate();
            _topicRoot = MqttTopics.NormalizeRoot(topicRoot);

            ConnectionStatusChanged?.Invoke(false, "Đang kết nối MQTT Broker...");
            _mqtt.ClearRememberedSubscriptions();
            await SubscribeWildcardTopicsAsync();
            await _mqtt.ConnectAsync(settings);
        }

        public Task DisconnectAsync() => _mqtt.DisconnectAsync();

        public async Task SubscribeWildcardTopicsAsync()
        {
            await _mqtt.SubscribeAsync(MqttTopics.TelemetryWildcard(_topicRoot), MqttQualityOfServiceLevel.AtLeastOnce);
            await _mqtt.SubscribeAsync(MqttTopics.StatusWildcard(_topicRoot), MqttQualityOfServiceLevel.AtLeastOnce);
        }

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

        public async ValueTask DisposeAsync()
        {
            _mqtt.ConnectionChangedAsync -= OnConnectionChangedAsync;
            _mqtt.ReconnectingAsync -= OnReconnectingAsync;
            _mqtt.MessageReceivedAsync -= OnMessageReceivedAsync;
            await _mqtt.DisposeAsync();
        }

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

        private Task OnMessageReceivedAsync(
            string topic,
            string payload,
            MqttQualityOfServiceLevel qos,
            bool retain)
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

        private void HandleTelemetry(string topic, string payload)
        {
            if (!MessageValidator.TryParseTelemetry(payload, out var message, out var error) || message == null)
            {
                Reject(error);
                return;
            }

            if (!MessageValidator.ValidateTopic(
                    topic,
                    "telemetry",
                    message.DeviceId,
                    message.DeviceType,
                    message.Location,
                    out error,
                    _topicRoot))
            {
                Reject(error);
                return;
            }

            if (!_telemetryFilter.TryAccept(message, out error))
            {
                Reject(error);
                return;
            }

            TelemetryReceived?.Invoke(message);
        }

        private void HandleStatus(string topic, string payload)
        {
            if (!MessageValidator.TryParseStatus(payload, out var status, out var error) || status == null)
            {
                Reject(error);
                return;
            }

            if (!MessageValidator.ValidateTopic(topic, "status", status.DeviceId, null, null, out error, _topicRoot))
            {
                Reject(error);
                return;
            }

            if (!_statusDeduplicator.TryAccept(status.MessageId))
            {
                Reject($"Duplicate status message: {status.MessageId}");
                return;
            }

            if (!_statusOrderingFilter.TryAccept(status.DeviceId, status.Timestamp, out error))
            {
                Reject(error);
                return;
            }

            DeviceStatusReceived?.Invoke(status);
        }

        private void Reject(string reason)
        {
            MessageRejected?.Invoke(reason);
            AppLogger.Warning("MESSAGE_REJECTED", reason);
        }
    }
}
