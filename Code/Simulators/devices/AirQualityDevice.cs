using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UDM_21.Shared;

namespace UDM_21.Simulators.Devices
{
    public class AirQualityDevice : DeviceBase
    {
        private readonly Random _rand = new Random();
        private double _aqi = 75.0;
        private double _co2 = 450.0;
        private bool _forceAlert = false;

        public AirQualityDevice(string deviceId, string location, int interval = 4, string topicRoot = MqttTopics.DefaultRoot)
            : base(deviceId, "sensor", location, interval, topicRoot) { }

        protected override Dictionary<string, object> GenerateTelemetry()
        {
            if (_forceAlert)
            {
                _aqi = Math.Round(180.0 + _rand.NextDouble() * 30.0, 1);
                _co2 = Math.Round(1200.0 + _rand.NextDouble() * 200.0, 1);
            }
            else
            {
                _aqi = Math.Max(10.0, Math.Round(_aqi + (_rand.NextDouble() * 6.0 - 3.0), 1));
                _co2 = Math.Max(300.0, Math.Round(_co2 + (_rand.NextDouble() * 10.0 - 5.0), 1));
            }

            string status = _aqi < 100 ? "GOOD" : (_aqi < 150 ? "MODERATE" : "POOR");

            return new Dictionary<string, object>
            {
                { "aqi", _aqi },
                { "co2_ppm", _co2 },
                { "air_status", status }
            };
        }

        protected override async Task HandleCommandAsync(CommandMessage cmd)
        {
            await base.HandleCommandAsync(cmd);
            bool stateChanged = false;

            if (cmd.Command == "SET_AQI")
            {
                if (cmd.Params.TryGetValue("aqi", out var aObj) &&
                    double.TryParse(aObj?.ToString(), out double newA))
                {
                    _aqi = newA;
                    _forceAlert = false;
                    stateChanged = true;
                }
            }
            else if (cmd.Command == "PURIFY_AIR")
            {
                _aqi = 35.0;
                _co2 = 400.0;
                _forceAlert = false;
                stateChanged = true;
            }
            else if (cmd.Command == "TRIGGER_POLLUTION_ALERT")
            {
                _forceAlert = true;
                stateChanged = true;
            }

            if (stateChanged)
            {
                Console.WriteLine($"[Device {DeviceId}] 🍃 PHẢN HỒI LỆNH: Cập nhật không khí -> AQI {_aqi} | CO2 {_co2}");
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
