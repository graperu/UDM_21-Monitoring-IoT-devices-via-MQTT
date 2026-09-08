using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UDM_21.Shared;

namespace UDM_21.Simulators.Devices
{
    public class PowerMeterDevice : DeviceBase
    {
        private readonly Random _rand = new Random();
        private double _voltage = 220.0;
        private double _current = 4.5;
        private double _totalKwh = 120.0;
        private bool _forceOverload = false;

        public PowerMeterDevice(string deviceId, string location, int interval = 2, string topicRoot = MqttTopics.DefaultRoot)
            : base(deviceId, "meter", location, interval, topicRoot) { }

        protected override Dictionary<string, object> GenerateTelemetry()
        {
            double v = Math.Round(_voltage + (_rand.NextDouble() * 4.0 - 2.0), 1);
            double i;
            if (_forceOverload)
            {
                i = Math.Round(16.0 + _rand.NextDouble() * 3.0, 2); // > 3500W
            }
            else
            {
                i = Math.Max(0.5, Math.Round(_current + (_rand.NextDouble() * 1.0 - 0.5), 2));
            }

            double powerW = Math.Round(v * i, 1);
            _totalKwh += Math.Round((powerW / 1000.0) * (PublishIntervalSeconds / 3600.0), 4);

            return new Dictionary<string, object>
            {
                { "voltage_v", v },
                { "current_a", i },
                { "power_watt", powerW },
                { "total_kwh", Math.Round(_totalKwh, 2) }
            };
        }

        protected override async Task HandleCommandAsync(CommandMessage cmd)
        {
            await base.HandleCommandAsync(cmd);
            bool stateChanged = false;

            if (cmd.Command == "RESET_ENERGY" || cmd.Command == "RESET_KWH")
            {
                _totalKwh = 0.0;
                _forceOverload = false;
                stateChanged = true;
            }
            else if (cmd.Command == "SET_LOAD" || cmd.Command == "SET_POWER")
            {
                if (cmd.Params.TryGetValue("power_watt", out var pObj) &&
                    double.TryParse(pObj?.ToString(), out double newP))
                {
                    _current = Math.Max(0.1, newP / _voltage);
                    _forceOverload = false;
                    stateChanged = true;
                }
            }
            else if (cmd.Command == "TRIGGER_OVERLOAD")
            {
                _forceOverload = true;
                stateChanged = true;
            }

            if (stateChanged)
            {
                Console.WriteLine($"[Device {DeviceId}] ⚡ PHẢN HỒI LỆNH: Cập nhật tải điện -> Overload: {_forceOverload} | kWh: {_totalKwh}");
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
                await Mqtt.PublishAsync(TelemetryTopic, msg.ToJson(), MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce);
            }
        }
    }
}
