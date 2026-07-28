using System;
using System.Collections.Generic;

namespace UDM_21.Simulators.Devices
{
    public class AirQualityDevice : DeviceBase
    {
        private readonly Random _rand = new Random();
        private double _aqi = 75.0;
        private double _co2 = 450.0;

        public AirQualityDevice(string deviceId, string location, int interval = 4)
            : base(deviceId, "sensor", location, interval) { }

        protected override Dictionary<string, object> GenerateTelemetry()
        {
            _aqi = Math.Max(10.0, Math.Round(_aqi + (_rand.NextDouble() * 10.0 - 5.0), 1));
            _co2 = Math.Max(300.0, Math.Round(_co2 + (_rand.NextDouble() * 20.0 - 10.0), 1));

            string status = _aqi < 100 ? "GOOD" : (_aqi < 150 ? "MODERATE" : "POOR");

            return new Dictionary<string, object>
            {
                { "aqi", _aqi },
                { "co2_ppm", _co2 },
                { "air_status", status }
            };
        }
    }
}
