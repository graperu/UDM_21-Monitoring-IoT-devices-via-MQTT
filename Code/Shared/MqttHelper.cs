using System;
using System.Collections.Concurrent;
using System.Text;
using System.Threading;
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
    // 5. Tự động kết nối lại (auto-reconnect) khi mất kết nối ngoài ý muốn (rớt mạng, Broker sập).
    // =========================================================================
    public class MqttHelper
    {
        private readonly IMqttClient _client;
        private readonly MqttFactory _factory;
        private readonly bool _cleanSession;
        public string ClientId { get; }

        // --- Thong tin ket noi duoc luu lai de dung cho viec reconnect ---
        private string _host = "localhost";
        private int _port = 1883;
        private string? _lastWillTopic;
        private string? _lastWillPayload;

        // --- Danh sach topic da subscribe (topic -> QoS), dung de subscribe lai sau reconnect ---
        private readonly ConcurrentDictionary<string, MqttQualityOfServiceLevel> _subscriptions = new();

        // --- Trang thai cho co che auto-reconnect ---
        private volatile bool _isManualDisconnect = false;
        private CancellationTokenSource? _reconnectCts;
        private readonly object _reconnectLock = new();

        // Thoi gian cho tang dan giua cac lan thu ket noi lai (giay): 2 -> 4 -> 8 -> 16 -> 30 (roi giu nguyen)
        private static readonly int[] BackoffDelaysSeconds = { 2, 4, 8, 16, 30 };

        public event Func<string, string, MqttQualityOfServiceLevel, bool, Task>? MessageReceivedAsync;
        public event Func<bool, Task>? ConnectionChangedAsync;

        // Su kien moi: bao hieu dang trong qua trinh thu ket noi lai (de UI hien "Dang ket noi lai... lan 3")
        public event Func<int, Task>? ReconnectingAsync;

        public MqttHelper(string clientId, bool cleanSession = true)
        {
            ClientId = clientId;
            _cleanSession = cleanSession;
            _factory = new MqttFactory();
            _client = _factory.CreateMqttClient();

            // Đăng ký sự kiện từ thư viện MQTTnet
            _client.ConnectedAsync += OnConnectedAsync;
            _client.DisconnectedAsync += OnDisconnectedAsync;
            _client.ApplicationMessageReceivedAsync += OnApplicationMessageReceivedAsync;
        }

        // Ham ket noi toi Broker (lan dau tien). Thong so duoc luu lai de tai su dung khi reconnect.
        public async Task ConnectAsync(string host = "localhost", int port = 1883, string? lastWillTopic = null, string? lastWillPayload = null)
        {
            _host = host;
            _port = port;
            _lastWillTopic = lastWillTopic;
            _lastWillPayload = lastWillPayload;
            _isManualDisconnect = false;

            var options = BuildOptions();
            await _client.ConnectAsync(options);
        }

        // Dung chung de tao MqttClientOptions cho ca lan ket noi dau va cac lan reconnect ve sau
        private MqttClientOptions BuildOptions()
        {
            var builder = new MqttClientOptionsBuilder()
                .WithClientId(ClientId)
                .WithTcpServer(_host, _port)
                .WithCleanSession(_cleanSession)
                .WithKeepAlivePeriod(TimeSpan.FromSeconds(30));

            if (!string.IsNullOrEmpty(_lastWillTopic) && !string.IsNullOrEmpty(_lastWillPayload))
            {
                builder.WithWillTopic(_lastWillTopic)
                       .WithWillPayload(Encoding.UTF8.GetBytes(_lastWillPayload))
                       .WithWillQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                       .WithWillRetain(true);
            }

            return builder.Build();
        }

        // Ngat ket noi CHU DONG (vd: tat thiet bi binh thuong). Se KHONG kich hoat auto-reconnect.
        public async Task DisconnectAsync()
        {
            _isManualDisconnect = true;
            StopReconnectLoop();

            if (_client.IsConnected)
            {
                await _client.DisconnectAsync();
            }
        }

        // Dang ky Topic (Subscribe) - ghi nho lai de tu dong subscribe lai sau khi reconnect
        public async Task SubscribeAsync(string topic, MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.AtLeastOnce)
        {
            _subscriptions[topic] = qos;

            var options = _factory.CreateSubscribeOptionsBuilder()
                .WithTopicFilter(f => f.WithTopic(topic).WithQualityOfServiceLevel(qos))
                .Build();

            await _client.SubscribeAsync(options);
        }

        // Gui tin (Publish)
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
            // Ket noi (hoac reconnect) thanh cong -> dung vong lap reconnect neu dang chay
            StopReconnectLoop();

            ConnectionChangedAsync?.Invoke(true);

            // Tu dong subscribe lai toan bo topic da dang ky truoc do
            // (bat buoc voi Clean Session = true, vi Broker se xoa subscription cu khi mat ket noi)
            _ = ResubscribeAllAsync();

            return Task.CompletedTask;
        }

        private async Task ResubscribeAllAsync()
        {
            foreach (var kv in _subscriptions)
            {
                try
                {
                    var options = _factory.CreateSubscribeOptionsBuilder()
                        .WithTopicFilter(f => f.WithTopic(kv.Key).WithQualityOfServiceLevel(kv.Value))
                        .Build();
                    await _client.SubscribeAsync(options);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[{ClientId}] Loi khi subscribe lai topic '{kv.Key}': {ex.Message}");
                }
            }
        }

        private Task OnDisconnectedAsync(MqttClientDisconnectedEventArgs args)
        {
            ConnectionChangedAsync?.Invoke(false);

            // Chi tu dong ket noi lai neu day la mat ket noi NGOAI Y MUON (rot mang, broker sap).
            // Neu la ngat ket noi CHU DONG (goi DisconnectAsync()) thi khong lam gi ca.
            if (!_isManualDisconnect)
            {
                StartReconnectLoop();
            }

            return Task.CompletedTask;
        }

        private void StartReconnectLoop()
        {
            lock (_reconnectLock)
            {
                if (_reconnectCts != null) return; // da co vong lap dang chay, khong tao them
                _reconnectCts = new CancellationTokenSource();
                _ = ReconnectLoopAsync(_reconnectCts.Token);
            }
        }

        private void StopReconnectLoop()
        {
            lock (_reconnectLock)
            {
                _reconnectCts?.Cancel();
                _reconnectCts = null;
            }
        }

        // Vong lap thu ket noi lai voi exponential backoff: 2s -> 4s -> 8s -> 16s -> 30s (roi giu nguyen 30s)
        private async Task ReconnectLoopAsync(CancellationToken token)
        {
            int attempt = 0;

            while (!token.IsCancellationRequested)
            {
                attempt++;
                int delaySeconds = BackoffDelaysSeconds[Math.Min(attempt - 1, BackoffDelaysSeconds.Length - 1)];

                Console.WriteLine($"[{ClientId}] Mat ket noi Broker. Se thu ket noi lai lan {attempt} sau {delaySeconds}s...");

                try
                {
                    ReconnectingAsync?.Invoke(attempt);
                    await Task.Delay(TimeSpan.FromSeconds(delaySeconds), token);
                }
                catch (TaskCanceledException)
                {
                    break; // vong lap bi huy (do reconnect thanh cong hoac ngat thu cong)
                }

                if (token.IsCancellationRequested) break;

                try
                {
                    var options = BuildOptions();
                    await _client.ConnectAsync(options);
                    // Neu thanh cong, OnConnectedAsync se duoc MQTTnet tu goi,
                    // trong do se goi StopReconnectLoop() de dung vong lap nay.
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[{ClientId}] Ket noi lai lan {attempt} that bai: {ex.Message}");
                    // Khong break - vong lap while se tu dong thu lai o lan tiep theo
                }
            }
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
