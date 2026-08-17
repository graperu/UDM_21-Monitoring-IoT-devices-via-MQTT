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

            Mqtt = new MqttHelper($"sim_{DeviceId}_{Guid.NewGuid().ToString("N").Substring(0, 4)}");
            Mqtt.MessageReceivedAsync += OnMessageReceivedAsync;

            // Moi khi ket noi (bao gom ca lan dau va cac lan RECONNECT sau khi rot mang),
            // tu dong publish lai trang thai "online" (retained) de Dashboard luon hien thi dung.
            Mqtt.ConnectionChangedAsync += OnMqttConnectionChangedAsync;
        }

        public async Task StartAsync(string brokerHost = "broker.emqx.io", int brokerPort = 1883)
        {
            _cts = new CancellationTokenSource();

            var lwtStatus = new DeviceStatusMessage 
            { 
                MessageId = Guid.NewGuid().ToString(),
                DeviceId = DeviceId, 
                Status = "offline",
                Timestamp = DateTime.UtcNow.ToString("o")
            };
            
            try
            {
                await Mqtt.ConnectAsync(brokerHost, brokerPort, StatusTopic, lwtStatus.ToJson());
                await Mqtt.SubscribeAsync(CmdTopic);
                Console.WriteLine($"[Device {DeviceId}] Online and active.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Device {DeviceId}] Unreachable broker '{brokerHost}:{brokerPort}' ({ex.Message}). Auto-reconnect active...");
            }

            // Start telemetry publish loop regardless, auto-reconnect will re-establish session when broker is ready
            _ = Task.Run(() => TelemetryLoopAsync(_cts.Token));
        }

        public async Task StopAsync()
        {
            _cts?.Cancel();
            var offlineStatus = new DeviceStatusMessage 
            { 
                MessageId = Guid.NewGuid().ToString(),
                DeviceId = DeviceId, 
                Status = "offline",
                Timestamp = DateTime.UtcNow.ToString("o")
            };
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
    }
}
