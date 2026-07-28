using System;
using System.Collections.Generic;
using UDM_21.Shared;

namespace UDM_21.Simulators.Devices
{
    public class TempHumidityDevice : DeviceBase
    {
        private readonly Random _rand = new Random();
        private double _baseTemp = 28.5;
        private double _baseHum = 65.0;

        public TempHumidityDevice(string deviceId, string location, int interval = 3)
            : base(deviceId, "sensor", location, interval) { }

        protected override Dictionary<string, object> GenerateTelemetry()
        {
            double temp = Math.Round(_baseTemp + (_rand.NextDouble() * 3.0 - 1.5), 2);
            double hum = Math.Round(_baseHum + (_rand.NextDouble() * 6.0 - 3.0), 2);

            return new Dictionary<string, object>
            {
                { "temperature", temp },
                { "humidity", hum },
                { "unit_temp", "°C" },
                { "unit_hum", "%" }
            };
        }
    }
}
