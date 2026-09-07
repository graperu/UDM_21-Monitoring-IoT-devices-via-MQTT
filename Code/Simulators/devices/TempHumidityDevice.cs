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
        private int _counter = 0;

        public TempHumidityDevice(string deviceId, string location, int interval = 3, string topicRoot = MqttTopics.DefaultRoot)
            : base(deviceId, "sensor", location, interval, topicRoot) { }

        protected override Dictionary<string, object> GenerateTelemetry()
        {
            _counter++;
            double temp;
            double hum = Math.Round(_baseHum + (_rand.NextDouble() * 6.0 - 3.0), 2);

            // Cứ mỗi 5 chu kỳ phát dữ liệu, phát 1 lần số liệu bất thường (Nhiệt độ > 40°C) để test cảnh báo
            if (_counter % 5 == 0)
            {
                temp = Math.Round(48.5 + _rand.NextDouble() * 6.0, 2); // 48.5°C - 54.5°C
                Console.WriteLine($"[Device {DeviceId}] ⚠️ CẢNH BÁO BẤT THƯỜNG: Phát hiện nhiệt độ tăng đột biến ({temp}°C > 40°C)!");
            }
            else
            {
                temp = Math.Round(_baseTemp + (_rand.NextDouble() * 3.0 - 1.5), 2);
            }

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
