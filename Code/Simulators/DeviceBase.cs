using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UDM_21.Shared;

namespace UDM_21.Simulators
{
    public abstract class DeviceBase
    {
        public string DeviceId { get; }
        public string DeviceType { get; }
        public string Location { get; }
        public int PublishIntervalSeconds { get; }

        protected string TelemetryTopic => $"iot/{Location}/{DeviceType}/{DeviceId}/telemetry";
        protected string StatusTopic => $"iot/{Location}/{DeviceType}/{DeviceId}/status";
        protected string CmdTopic => $"iot/{Location}/{DeviceType}/{DeviceId}/cmd";

        protected readonly MqttHelper Mqtt;
        private CancellationTokenSource? _cts;

        protected DeviceBase(string deviceId, string deviceType, string location, int publishIntervalSeconds = 3)
        {
            DeviceId = deviceId;
            DeviceType = deviceType;
            Location = location;
            PublishIntervalSeconds = publishIntervalSeconds;

            Mqtt = new MqttHelper($"sim_{DeviceId}");
            Mqtt.MessageReceivedAsync += OnMessageReceivedAsync;
        }

        public async Task StartAsync(string brokerHost = "localhost", int brokerPort = 1883)
        {
            _cts = new CancellationTokenSource();

            var lwtStatus = new DeviceStatusMessage { DeviceId = DeviceId, Status = "offline" };
            await Mqtt.ConnectAsync(brokerHost, brokerPort, StatusTopic, lwtStatus.ToJson());

            // Publish retained online status
            var onlineStatus = new DeviceStatusMessage { DeviceId = DeviceId, Status = "online" };
            await Mqtt.PublishAsync(StatusTopic, onlineStatus.ToJson(), MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce, retain: true);

            // Subscribe to command topic
            await Mqtt.SubscribeAsync(CmdTopic);

            // Start telemetry publish loop
            _ = Task.Run(() => TelemetryLoopAsync(_cts.Token));
            Console.WriteLine($"[Device {DeviceId}] Online and active.");
        }

        public async Task StopAsync()
        {
            _cts?.Cancel();
            var offlineStatus = new DeviceStatusMessage { DeviceId = DeviceId, Status = "offline" };
            await Mqtt.PublishAsync(StatusTopic, offlineStatus.ToJson(), MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce, retain: true);
            await Mqtt.DisconnectAsync();
            Console.WriteLine($"[Device {DeviceId}] Offline and stopped.");
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
                        DeviceId = DeviceId,
                        DeviceType = DeviceType,
                        Location = Location,
                        Data = data
                    };

                    await Mqtt.PublishAsync(TelemetryTopic, msg.ToJson(), MQTTnet.Protocol.MqttQualityOfServiceLevel.AtMostOnce);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Device {DeviceId}] Telemetry error: {ex.Message}");
                }

                await Task.Delay(TimeSpan.FromSeconds(PublishIntervalSeconds), token);
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
            if (topic == CmdTopic)
            {
                var cmd = CommandMessage.FromJson(payload);
                if (cmd != null)
                {
                    HandleCommandAsync(cmd);
                }
            }
            return Task.CompletedTask;
        }
    }
}
