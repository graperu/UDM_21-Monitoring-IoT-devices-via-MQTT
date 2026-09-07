using System;
using System.Collections.Generic;
using UDM_21.Shared;

namespace UDM_21.Simulators.Devices
{
    public class DoorSensorDevice : DeviceBase
    {
        private readonly Random _rand = new Random();
        private string _doorState = "CLOSED";

        public DoorSensorDevice(string deviceId, string location, int interval = 5, string topicRoot = MqttTopics.DefaultRoot)
            : base(deviceId, "security", location, interval, topicRoot) { }

        protected override Dictionary<string, object> GenerateTelemetry()
        {
            if (_rand.NextDouble() < 0.15)
            {
                _doorState = _doorState == "CLOSED" ? "OPEN" : "CLOSED";
            }

            return new Dictionary<string, object>
            {
                { "door_state", _doorState },
                { "battery_pct", 98 },
                { "tamper_alert", false }
            };
        }
    }
}
