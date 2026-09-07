using System;
using System.Collections.Generic;
using UDM_21.Shared;

namespace UDM_21.Simulators.Devices
{
    public class PowerMeterDevice : DeviceBase
    {
        private readonly Random _rand = new Random();
        private double _voltage = 220.0;
        private double _current = 4.5;
        private double _totalKwh = 120.0;

        public PowerMeterDevice(string deviceId, string location, int interval = 2, string topicRoot = MqttTopics.DefaultRoot)
            : base(deviceId, "meter", location, interval, topicRoot) { }

        protected override Dictionary<string, object> GenerateTelemetry()
        {
            double v = Math.Round(_voltage + (_rand.NextDouble() * 4.0 - 2.0), 1);
            double i = Math.Max(0.5, Math.Round(_current + (_rand.NextDouble() * 1.0 - 0.5), 2));
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
    }
}
