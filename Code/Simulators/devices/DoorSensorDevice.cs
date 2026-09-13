using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UDM_21.Shared;

namespace UDM_21.Simulators.Devices
{
    public class DoorSensorDevice : DeviceBase
    {
        private readonly Random _rand = new Random();
        private string _doorState = "CLOSED";
        private bool _tamperAlert = false;
        private int _batteryPct = 98;

        public DoorSensorDevice(string deviceId, string location, int interval = 5, string topicRoot = MqttTopics.DefaultRoot)
            : base(deviceId, "security", location, interval, topicRoot) { }

        protected override Dictionary<string, object> GenerateTelemetry()
        {
            return new Dictionary<string, object>
            {
                { "door_state", _doorState },
                { "battery_pct", _batteryPct },
                { "tamper_alert", _tamperAlert }
            };
        }

        protected override async Task HandleCommandAsync(CommandMessage cmd)
        {
            await base.HandleCommandAsync(cmd);
            bool stateChanged = false;

            switch (cmd.Command)
            {
                case "OPEN_DOOR":
                    _doorState = "OPEN";
                    stateChanged = true;
                    break;
                case "CLOSE_DOOR":
                    _doorState = "CLOSED";
                    stateChanged = true;
                    break;
                case "TOGGLE_DOOR":
                    _doorState = _doorState == "CLOSED" ? "OPEN" : "CLOSED";
                    stateChanged = true;
                    break;
                case "SET_DOOR_STATE":
                    if (cmd.Params.TryGetValue("door_state", out var ds) && ds != null)
                    {
                        _doorState = ds.ToString()?.ToUpperInvariant() ?? _doorState;
                        stateChanged = true;
                    }
                    break;
                case "TRIGGER_ALARM":
                    _tamperAlert = true;
                    stateChanged = true;
                    break;
                case "CLEAR_ALARM":
                    _tamperAlert = false;
                    stateChanged = true;
                    break;
            }

            if (stateChanged)
            {
                Console.WriteLine($"[Device {DeviceId}] 🚪 PHẢN HỒI LỆNH: Cửa hiện đang [{_doorState}] | Cảnh báo: {_tamperAlert}");
                await PublishTelemetryAsync(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce);
            }
        }
    }
}
