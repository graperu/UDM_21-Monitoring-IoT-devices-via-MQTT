using System;
using System.Threading.Tasks;
using UDM_21.Shared;

namespace UDM_21.Dashboard.Controllers
{
    public class MqttController
    {
        private readonly MqttHelper _mqtt;

        public event Action<bool, string>? ConnectionStatusChanged;
        public event Action<TelemetryMessage>? TelemetryReceived;
        public event Action<DeviceStatusMessage>? DeviceStatusReceived;

        public MqttController(string? clientId = null)
        {
            string uniqueId = clientId ?? $"WpfDashboard_{Guid.NewGuid().ToString("N").Substring(0, 6)}";
            _mqtt = new MqttHelper(uniqueId, cleanSession: true);
            _mqtt.ConnectionChangedAsync += OnConnectionChangedAsync;
            _mqtt.ReconnectingAsync += OnReconnectingAsync;
            _mqtt.MessageReceivedAsync += OnMessageReceivedAsync;
        }

        public async Task ConnectAsync(string host = "localhost", int port = 1883)
        {
            ConnectionStatusChanged?.Invoke(false, "Đang kết nối MQTT Broker...");
            await _mqtt.ConnectAsync(host, port);
        }

        public async Task DisconnectAsync()
        {
            await _mqtt.DisconnectAsync();
        }

        public async Task SubscribeWildcardTopicsAsync()
        {
            await _mqtt.SubscribeAsync("iot/+/+/+/telemetry");
            await _mqtt.SubscribeAsync("iot/+/+/+/status");
        }

        public async Task SendCommandAsync(string location, string deviceType, string deviceId, string command, System.Collections.Generic.Dictionary<string, object> parameters)
        {
            string topic = $"iot/{location}/{deviceType}/{deviceId}/cmd";
            var cmdObj = new CommandMessage
            {
                Command = command,
                Params = parameters
            };

            await _mqtt.PublishAsync(topic, cmdObj.ToJson(), MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce);
        }

        private async Task OnConnectionChangedAsync(bool isConnected)
        {
            if (isConnected)
            {
                ConnectionStatusChanged?.Invoke(true, "Đã kết nối thành công!");
                // Luu y: MqttHelper da tu dong subscribe lai cac topic sau reconnect,
                // nen goi lai o day (lan dau + cac lan sau) van an toan, khong gay loi hay trung du lieu.
                await SubscribeWildcardTopicsAsync();
            }
            else
            {
                ConnectionStatusChanged?.Invoke(false, "Mất kết nối với MQTT Broker! Đang thử kết nối lại...");
            }
        }

        // Duoc MqttHelper goi moi lan chuan bi thu ket noi lai, kem so lan da thu
        private Task OnReconnectingAsync(int attempt)
        {
            ConnectionStatusChanged?.Invoke(false, $"Mất kết nối - đang thử kết nối lại (lần {attempt})...");
            return Task.CompletedTask;
        }

        private Task OnMessageReceivedAsync(string topic, string payload, MQTTnet.Protocol.MqttQualityOfServiceLevel qos, bool retain)
        {
            if (topic.EndsWith("/telemetry"))
            {
                var msg = TelemetryMessage.FromJson(payload);
                if (msg != null)
                {
                    TelemetryReceived?.Invoke(msg);
                }
            }
            else if (topic.EndsWith("/status"))
            {
                var statusMsg = DeviceStatusMessage.FromJson(payload);
                if (statusMsg != null)
                {
                    DeviceStatusReceived?.Invoke(statusMsg);
                }
            }
            return Task.CompletedTask;
        }
    }
}
