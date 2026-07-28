using System;
using System.Text;
using System.Threading.Tasks;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Protocol;

namespace UDM_21.Shared
{
    // =========================================================================
    // TODO (Thành viên 3): XỬ LÝ MẠNG VÀ GIAO THỨC MQTT
    // Nhiệm vụ:
    // 1. Quản lý kết nối, ngắt kết nối với MQTT Broker.
    // 2. Thiết lập LWT (Last Will and Testament) để thông báo khi thiết bị ngắt kết nối đột ngột.
    // 3. Đăng ký nhận tin (Subscribe) hỗ trợ Wildcard.
    // 4. Phát tin (Publish) dữ liệu cảm biến và lệnh điều khiển với QoS phù hợp (QoS 0, QoS 1).
    // =========================================================================
    public class MqttHelper
    {
        private readonly IMqttClient _client;
        private readonly MqttFactory _factory;
        public string ClientId { get; }

        public event Func<string, string, MqttQualityOfServiceLevel, bool, Task>? MessageReceivedAsync;
        public event Func<bool, Task>? ConnectionChangedAsync;

        public MqttHelper(string clientId, bool cleanSession = true)
        {
            ClientId = clientId;
            _factory = new MqttFactory();
            _client = _factory.CreateMqttClient();

            // Đăng ký sự kiện từ thư viện MQTTnet
            _client.ConnectedAsync += OnConnectedAsync;
            _client.DisconnectedAsync += OnDisconnectedAsync;
            _client.ApplicationMessageReceivedAsync += OnApplicationMessageReceivedAsync;
        }

        // TODO: Viết hàm kết nối tới Broker
        public async Task ConnectAsync(string host = "localhost", int port = 1883, string? lastWillTopic = null, string? lastWillPayload = null)
        {
            var builder = new MqttClientOptionsBuilder()
                .WithClientId(ClientId)
                .WithTcpServer(host, port)
                .WithCleanSession(true);

            // TODO: Cấu hình LWT (Last Will) khi có tham số
            if (!string.IsNullOrEmpty(lastWillTopic) && !string.IsNullOrEmpty(lastWillPayload))
            {
                builder.WithWillTopic(lastWillTopic)
                       .WithWillPayload(Encoding.UTF8.GetBytes(lastWillPayload))
                       .WithWillQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                       .WithWillRetain(true);
            }

            var options = builder.Build();
            await _client.ConnectAsync(options);
        }

        // TODO: Viết hàm ngắt kết nối an toàn
        public async Task DisconnectAsync()
        {
            if (_client.IsConnected)
            {
                await _client.DisconnectAsync();
            }
        }

        // TODO: Viết hàm đăng ký Topic (Subscribe)
        public async Task SubscribeAsync(string topic, MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.AtLeastOnce)
        {
            var options = _factory.CreateSubscribeOptionsBuilder()
                .WithTopicFilter(f => f.WithTopic(topic).WithQualityOfServiceLevel(qos))
                .Build();

            await _client.SubscribeAsync(options);
        }

        // TODO: Viết hàm gửi tin (Publish)
        public async Task PublishAsync(string topic, string payload, MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.AtLeastOnce, bool retain = false)
        {
            var message = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(Encoding.UTF8.GetBytes(payload))
                .WithQualityOfServiceLevel(qos)
                .WithRetainFlag(retain)
                .Build();

            await _client.PublishAsync(message);
        }

        private Task OnConnectedAsync(MqttClientConnectedEventArgs args)
        {
            ConnectionChangedAsync?.Invoke(true);
            return Task.CompletedTask;
        }

        private Task OnDisconnectedAsync(MqttClientDisconnectedEventArgs args)
        {
            ConnectionChangedAsync?.Invoke(false);
            return Task.CompletedTask;
        }

        private Task OnApplicationMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs args)
        {
            var topic = args.ApplicationMessage.Topic;
            var payload = Encoding.UTF8.GetString(args.ApplicationMessage.PayloadSegment);
            var qos = args.ApplicationMessage.QualityOfServiceLevel;
            var retain = args.ApplicationMessage.Retain;

            MessageReceivedAsync?.Invoke(topic, payload, qos, retain);
            return Task.CompletedTask;
        }
    }
}
