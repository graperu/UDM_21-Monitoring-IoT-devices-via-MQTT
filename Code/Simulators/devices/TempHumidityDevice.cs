using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UDM_21.Shared;

namespace UDM_21.Simulators.Devices
{
    public class TempHumidityDevice : DeviceBase
    {
        private readonly Random _rand = new Random();
        private double _baseTemp = 28.5;
        private double _baseHum = 65.0;
        private bool _forceAlert = false;

        public TempHumidityDevice(string deviceId, string location, int interval = 3, string topicRoot = MqttTopics.DefaultRoot)
            : base(deviceId, "sensor", location, interval, topicRoot) { }

        protected override Dictionary<string, object> GenerateTelemetry()
        {
            double hum = Math.Round(_baseHum + (_rand.NextDouble() * 4.0 - 2.0), 2);
            double temp = _forceAlert
                ? Math.Round(50.0 + _rand.NextDouble() * 5.0, 2)
                : Math.Round(_baseTemp + (_rand.NextDouble() * 2.0 - 1.0), 2);

            return new Dictionary<string, object>
            {
                { "temperature", temp },
                { "humidity", hum },
                { "unit_temp", "°C" },
                { "unit_hum", "%" }
            };
        }

        protected override async Task HandleCommandAsync(CommandMessage cmd)
        {
            await base.HandleCommandAsync(cmd);
            bool stateChanged = false;

            if (cmd.Command == "SET_TEMPERATURE")
            {
                if (cmd.Params.TryGetValue("temperature", out var tObj) &&
                    MessageValidator.TryGetFiniteDouble(tObj, out double newT))
                {
                    _baseTemp = newT;
                    _forceAlert = false;
                    stateChanged = true;
                }
            }
            else if (cmd.Command == "TRIGGER_HEAT_ALERT")
            {
                _forceAlert = true;
                stateChanged = true;
            }
            else if (cmd.Command == "CALIBRATE")
            {
                _baseTemp = 26.0;
                _baseHum = 60.0;
                _forceAlert = false;
                stateChanged = true;
            }

            if (stateChanged)
            {
                Console.WriteLine($"[Device {DeviceId}] 🌡️ PHẢN HỒI LỆNH: Nhiệt độ -> {_baseTemp}°C (ForceAlert: {_forceAlert})");
                await PublishTelemetryAsync(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce);
            }
        }
    }
}
