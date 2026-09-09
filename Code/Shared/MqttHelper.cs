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
    public sealed class MqttHelper : IAsyncDisposable
    {
        private static readonly int[] BackoffDelaysSeconds = { 2, 4, 8, 16, 30 };
        private static readonly TimeSpan OperationTimeout = TimeSpan.FromSeconds(10);

        private readonly IMqttClient _client;
        private readonly MqttFactory _factory;
        private readonly bool _cleanSession;
        private readonly ConcurrentDictionary<string, MqttQualityOfServiceLevel> _subscriptions = new();
        private readonly object _reconnectLock = new();

        private string _host = "localhost";
        private int _port = 1883;
        private bool _useTls;
        private string? _username;
        private string? _password;
        private string? _lastWillTopic;
        private string? _lastWillPayload;
        private volatile bool _isManualDisconnect;
        private volatile bool _isDisposed;
        private CancellationTokenSource? _reconnectCts;

        public string ClientId { get; }
        public bool IsConnected => _client.IsConnected;
        public bool IsManualDisconnect => _isManualDisconnect;

        public event Func<string, string, MqttQualityOfServiceLevel, bool, Task>? MessageReceivedAsync;
        public event Func<bool, Task>? ConnectionChangedAsync;
        public event Func<int, Task>? ReconnectingAsync;

        public MqttHelper(string clientId, bool cleanSession = true)
        {
            if (string.IsNullOrWhiteSpace(clientId)) throw new ArgumentException("Client ID không được để trống.", nameof(clientId));

            ClientId = clientId;
            _cleanSession = cleanSession;
            _factory = new MqttFactory();
            _client = _factory.CreateMqttClient();
            _client.ConnectedAsync += OnConnectedAsync;
            _client.DisconnectedAsync += OnDisconnectedAsync;
            _client.ApplicationMessageReceivedAsync += OnApplicationMessageReceivedAsync;
        }

        public async Task ConnectAsync(
            string host = "localhost",
            int port = 1883,
            string? lastWillTopic = null,
            string? lastWillPayload = null,
            CancellationToken cancellationToken = default)
        {
            await ConnectAsync(
                new MqttConnectionSettings { Host = host, Port = port },
                lastWillTopic,
                lastWillPayload,
                cancellationToken);
        }

        public async Task ConnectAsync(
            MqttConnectionSettings settings,
            string? lastWillTopic = null,
            string? lastWillPayload = null,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            ArgumentNullException.ThrowIfNull(settings);
            settings.Validate();
            StopReconnectLoop();

            _host = settings.Host.Trim();
            _port = settings.Port;
            _useTls = settings.UseTls;
            _username = string.IsNullOrWhiteSpace(settings.Username) ? null : settings.Username.Trim();
            _password = settings.Password;
            _lastWillTopic = lastWillTopic;
            _lastWillPayload = lastWillPayload;
            _isManualDisconnect = false;

            try
            {
                await ConnectClientWithTimeoutAsync(cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                AppLogger.Error("MQTT_CONNECT_FAILED", ex);
                StartReconnectLoop();
                throw;
            }
        }

        public async Task DisconnectAsync(CancellationToken cancellationToken = default)
        {
            if (_isDisposed) return;

            _isManualDisconnect = true;
            StopReconnectLoop();

            if (_client.IsConnected)
            {
                using var timeout = CreateTimeout(cancellationToken);
                var options = new MqttClientDisconnectOptionsBuilder().Build();
                await _client.DisconnectAsync(options, timeout.Token);
            }

            AppLogger.Info("MQTT_DISCONNECT", $"client={ClientId}; manual=true");
        }

        public async Task SubscribeAsync(
            string topic,
            MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.AtLeastOnce,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            if (string.IsNullOrWhiteSpace(topic)) throw new ArgumentException("Topic không được để trống.", nameof(topic));

            _subscriptions[topic] = qos;
            if (!_client.IsConnected) return;

            await SubscribeClientAsync(topic, qos, cancellationToken);
        }

        public void ClearRememberedSubscriptions()
        {
            ThrowIfDisposed();
            if (_client.IsConnected)
                throw new InvalidOperationException("Phải ngắt kết nối trước khi thay đổi toàn bộ subscription.");

            _subscriptions.Clear();
        }

        public async Task PublishAsync(
            string topic,
            string payload,
            MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.AtLeastOnce,
            bool retain = false,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            if (!_client.IsConnected) throw new InvalidOperationException("MQTT client chưa kết nối.");
            if (string.IsNullOrWhiteSpace(topic)) throw new ArgumentException("Topic không được để trống.", nameof(topic));
            if (payload == null) throw new ArgumentNullException(nameof(payload));

            var message = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(Encoding.UTF8.GetBytes(payload))
                .WithQualityOfServiceLevel(qos)
                .WithRetainFlag(retain)
                .Build();

            using var timeout = CreateTimeout(cancellationToken);
            await _client.PublishAsync(message, timeout.Token);
        }

        public async ValueTask DisposeAsync()
        {
            if (_isDisposed) return;

            try
            {
                await DisconnectAsync();
            }
            catch (Exception ex)
            {
                AppLogger.Error("MQTT_DISPOSE_DISCONNECT_FAILED", ex);
            }

            _isDisposed = true;
            _client.ConnectedAsync -= OnConnectedAsync;
            _client.DisconnectedAsync -= OnDisconnectedAsync;
            _client.ApplicationMessageReceivedAsync -= OnApplicationMessageReceivedAsync;
            _client.Dispose();
            _password = null;
        }

        private MqttClientOptions BuildOptions()
        {
            var builder = new MqttClientOptionsBuilder()
                .WithClientId(ClientId)
                .WithTcpServer(_host, _port)
                .WithCleanSession(_cleanSession)
                .WithKeepAlivePeriod(TimeSpan.FromSeconds(30));

            if (_username != null)
            {
                builder.WithCredentials(_username, _password ?? string.Empty);
            }

            if (_useTls)
            {
                // Use the operating system trust store; invalid broker certificates remain rejected.
                builder.WithTlsOptions(options => options.UseTls(true));
            }

            if (!string.IsNullOrEmpty(_lastWillTopic) && !string.IsNullOrEmpty(_lastWillPayload))
            {
                builder.WithWillTopic(_lastWillTopic)
                    .WithWillPayload(Encoding.UTF8.GetBytes(_lastWillPayload))
                    .WithWillQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                    .WithWillRetain(true);
            }

            return builder.Build();
        }

        private async Task ConnectClientWithTimeoutAsync(CancellationToken cancellationToken)
        {
            using var timeout = CreateTimeout(cancellationToken);
            await _client.ConnectAsync(BuildOptions(), timeout.Token);
        }

        private async Task SubscribeClientAsync(
            string topic,
            MqttQualityOfServiceLevel qos,
            CancellationToken cancellationToken)
        {
            var options = _factory.CreateSubscribeOptionsBuilder()
                .WithTopicFilter(filter => filter.WithTopic(topic).WithQualityOfServiceLevel(qos))
                .Build();

            using var timeout = CreateTimeout(cancellationToken);
            await _client.SubscribeAsync(options, timeout.Token);
        }

        private async Task OnConnectedAsync(MqttClientConnectedEventArgs args)
        {
            StopReconnectLoop();
            await ResubscribeAllAsync();
            await InvokeConnectionChangedAsync(true);
            AppLogger.Info(
                "MQTT_CONNECTED",
                $"client={ClientId}; endpoint={(_useTls ? "mqtts" : "mqtt")}://{_host}:{_port}; authenticated={_username != null}");
        }

        private async Task ResubscribeAllAsync()
        {
            foreach (var subscription in _subscriptions)
            {
                try
                {
                    await SubscribeClientAsync(subscription.Key, subscription.Value, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    AppLogger.Error("MQTT_RESUBSCRIBE_FAILED", ex);
                }
            }
        }

        private async Task OnDisconnectedAsync(MqttClientDisconnectedEventArgs args)
        {
            await InvokeConnectionChangedAsync(false);
            AppLogger.Warning(
                "MQTT_DISCONNECTED",
                $"client={ClientId}; manual={_isManualDisconnect}; reason={args.Reason}");

            if (!_isManualDisconnect && !_isDisposed)
            {
                StartReconnectLoop();
            }
        }

        private void StartReconnectLoop()
        {
            lock (_reconnectLock)
            {
                if (_reconnectCts != null || _isManualDisconnect || _isDisposed) return;

                _reconnectCts = new CancellationTokenSource();
                _ = ReconnectLoopAsync(_reconnectCts);
            }
        }

        private void StopReconnectLoop()
        {
            CancellationTokenSource? reconnectCts;
            lock (_reconnectLock)
            {
                reconnectCts = _reconnectCts;
                _reconnectCts = null;
            }

            reconnectCts?.Cancel();
        }

        private async Task ReconnectLoopAsync(CancellationTokenSource owner)
        {
            var attempt = 0;
            try
            {
                while (!owner.IsCancellationRequested && !_isManualDisconnect && !_isDisposed)
                {
                    attempt++;
                    var delaySeconds = BackoffDelaysSeconds[Math.Min(attempt - 1, BackoffDelaysSeconds.Length - 1)];
                    await InvokeReconnectingAsync(attempt);

                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(delaySeconds), owner.Token);
                        await ConnectClientWithTimeoutAsync(owner.Token);
                    }
                    catch (OperationCanceledException) when (owner.IsCancellationRequested)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        AppLogger.Error("MQTT_RECONNECT_FAILED", ex);
                    }
                }
            }
            finally
            {
                lock (_reconnectLock)
                {
                    if (ReferenceEquals(_reconnectCts, owner)) _reconnectCts = null;
                }

                owner.Dispose();
            }
        }

        private async Task OnApplicationMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs args)
        {
            var handler = MessageReceivedAsync;
            if (handler == null) return;

            var topic = args.ApplicationMessage.Topic;
            var payload = Encoding.UTF8.GetString(args.ApplicationMessage.PayloadSegment);
            var qos = args.ApplicationMessage.QualityOfServiceLevel;
            var retain = args.ApplicationMessage.Retain;

            foreach (Func<string, string, MqttQualityOfServiceLevel, bool, Task> subscriber in handler.GetInvocationList())
            {
                await subscriber(topic, payload, qos, retain);
            }
        }

        private async Task InvokeConnectionChangedAsync(bool isConnected)
        {
            var handler = ConnectionChangedAsync;
            if (handler == null) return;

            foreach (Func<bool, Task> subscriber in handler.GetInvocationList())
            {
                await subscriber(isConnected);
            }
        }

        private async Task InvokeReconnectingAsync(int attempt)
        {
            var handler = ReconnectingAsync;
            if (handler == null) return;

            foreach (Func<int, Task> subscriber in handler.GetInvocationList())
            {
                await subscriber(attempt);
            }
        }

        private static CancellationTokenSource CreateTimeout(CancellationToken cancellationToken)
        {
            var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(OperationTimeout);
            return timeout;
        }

        private void ThrowIfDisposed()
        {
            if (_isDisposed) throw new ObjectDisposedException(nameof(MqttHelper));
        }
    }
}
