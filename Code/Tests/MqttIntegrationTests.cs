using System.Net;
using System.Net.Sockets;
using System.Text;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Protocol;
using MQTTnet.Server;
using UDM_21.Shared;

namespace UDM_21.Tests;

public class MqttIntegrationTests
{
    [Fact(Timeout = 15000)]
    public async Task BrokerAuthenticationAcceptsValidCredentialsAndRejectsInvalidCredentials()
    {
        await using var broker = await EmbeddedBroker.StartAsync();
        broker.RequireCredentials("demo-user", "demo-password");

        await using var validClient = new MqttHelper($"auth_valid_{Guid.NewGuid():N}");
        await validClient.ConnectAsync(new MqttConnectionSettings
        {
            Host = IPAddress.Loopback.ToString(),
            Port = broker.Port,
            Username = "demo-user",
            Password = "demo-password"
        });
        Assert.True(validClient.IsConnected);

        await using var invalidClient = new MqttHelper($"auth_invalid_{Guid.NewGuid():N}");
        await Assert.ThrowsAnyAsync<Exception>(() => invalidClient.ConnectAsync(new MqttConnectionSettings
        {
            Host = IPAddress.Loopback.ToString(),
            Port = broker.Port,
            Username = "demo-user",
            Password = "wrong-password"
        }));
        Assert.False(invalidClient.IsConnected);
    }

    [Fact(Timeout = 20000)]
    public async Task AbruptClientLossPublishesRetainedLastWill()
    {
        await using var broker = await EmbeddedBroker.StartAsync();
        var willReceived = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        await using var observer = new MqttHelper($"observer_{Guid.NewGuid():N}");
        observer.MessageReceivedAsync += (topic, payload, qos, retain) =>
        {
            if (topic == "udm21_test/lab/sensor/device_lwt/status") willReceived.TrySetResult(payload);
            return Task.CompletedTask;
        };
        await observer.SubscribeAsync("udm21_test/lab/sensor/device_lwt/status");
        await observer.ConnectAsync(IPAddress.Loopback.ToString(), broker.Port);

        var factory = new MqttFactory();
        var device = factory.CreateMqttClient();
        var willPayload = "{\"status\":\"offline\"}";
        var options = new MqttClientOptionsBuilder()
            .WithClientId($"lwt_device_{Guid.NewGuid():N}")
            .WithTcpServer(IPAddress.Loopback.ToString(), broker.Port)
            .WithWillTopic("udm21_test/lab/sensor/device_lwt/status")
            .WithWillPayload(willPayload)
            .WithWillQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
            .WithWillRetain(true)
            .Build();

        await device.ConnectAsync(options, CancellationToken.None);
        device.Dispose();

        var payload = await willReceived.Task.WaitAsync(TimeSpan.FromSeconds(8));
        Assert.Equal(willPayload, payload);

        var retainedReceived = new TaskCompletionSource<(string Payload, bool Retain)>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        await using var lateObserver = new MqttHelper($"late_observer_{Guid.NewGuid():N}");
        lateObserver.MessageReceivedAsync += (topic, retainedPayload, qos, retain) =>
        {
            if (topic == "udm21_test/lab/sensor/device_lwt/status")
            {
                retainedReceived.TrySetResult((retainedPayload, retain));
            }

            return Task.CompletedTask;
        };
        await lateObserver.SubscribeAsync("udm21_test/lab/sensor/device_lwt/status");
        await lateObserver.ConnectAsync(IPAddress.Loopback.ToString(), broker.Port);

        var retained = await retainedReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(willPayload, retained.Payload);
        Assert.True(retained.Retain);
    }

    [Fact(Timeout = 25000)]
    public async Task BrokerLossIsDetectedThenClientReconnectsAndResubscribes()
    {
        var port = EmbeddedBroker.GetFreePort();
        await using var broker = await EmbeddedBroker.StartAsync(port);
        var disconnected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var reconnected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var messageReceived = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var connectionCount = 0;

        await using var subscriber = new MqttHelper($"reconnect_{Guid.NewGuid():N}");
        subscriber.ConnectionChangedAsync += connected =>
        {
            if (connected && Interlocked.Increment(ref connectionCount) >= 2) reconnected.TrySetResult();
            if (!connected) disconnected.TrySetResult();
            return Task.CompletedTask;
        };
        subscriber.MessageReceivedAsync += (topic, payload, qos, retain) =>
        {
            if (topic == "udm21_test/test/reconnect/telemetry") messageReceived.TrySetResult(payload);
            return Task.CompletedTask;
        };

        await subscriber.SubscribeAsync("udm21_test/test/reconnect/telemetry");
        await subscriber.ConnectAsync(IPAddress.Loopback.ToString(), port);
        await broker.StopAsync();
        await disconnected.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await broker.StartAsync();
        await reconnected.Task.WaitAsync(TimeSpan.FromSeconds(12));

        var factory = new MqttFactory();
        using var publisher = factory.CreateMqttClient();
        var options = new MqttClientOptionsBuilder()
            .WithClientId($"publisher_{Guid.NewGuid():N}")
            .WithTcpServer(IPAddress.Loopback.ToString(), port)
            .Build();
        await publisher.ConnectAsync(options, CancellationToken.None);
        await publisher.PublishAsync(
            new MqttApplicationMessageBuilder()
                .WithTopic("udm21_test/test/reconnect/telemetry")
                .WithPayload(Encoding.UTF8.GetBytes("reconnected"))
                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                .Build(),
            CancellationToken.None);

        Assert.Equal("reconnected", await messageReceived.Task.WaitAsync(TimeSpan.FromSeconds(5)));
    }

    private sealed class EmbeddedBroker : IAsyncDisposable
    {
        private readonly MqttServer _server;
        public int Port { get; }

        private EmbeddedBroker(int port)
        {
            Port = port;
            var options = new MqttServerOptionsBuilder()
                .WithDefaultEndpoint()
                .WithDefaultEndpointBoundIPAddress(IPAddress.Loopback)
                .WithDefaultEndpointPort(port)
                .Build();
            _server = new MqttFactory().CreateMqttServer(options);
        }

        public static async Task<EmbeddedBroker> StartAsync(int? port = null)
        {
            var broker = new EmbeddedBroker(port ?? GetFreePort());
            await broker.StartAsync();
            return broker;
        }

        public Task StartAsync() => _server.StartAsync();

        public Task StopAsync() => _server.IsStarted
            ? _server.StopAsync(new MqttServerStopOptionsBuilder().Build())
            : Task.CompletedTask;

        public void RequireCredentials(string username, string password)
        {
            _server.ValidatingConnectionAsync += args =>
            {
                if (!string.Equals(args.UserName, username, StringComparison.Ordinal) ||
                    !string.Equals(args.Password, password, StringComparison.Ordinal))
                {
                    args.ReasonCode = MqttConnectReasonCode.BadUserNameOrPassword;
                }

                return Task.CompletedTask;
            };
        }

        public async ValueTask DisposeAsync()
        {
            await StopAsync();
            _server.Dispose();
        }

        public static int GetFreePort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }
    }
}
