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

            if (cmd.Command == "OPEN_DOOR")
            {
                _doorState = "OPEN";
                stateChanged = true;
            }
            else if (cmd.Command == "CLOSE_DOOR")
            {
                _doorState = "CLOSED";
                stateChanged = true;
            }
            else if (cmd.Command == "TOGGLE_DOOR")
            {
                _doorState = _doorState == "CLOSED" ? "OPEN" : "CLOSED";
                stateChanged = true;
            }
            else if (cmd.Command == "SET_DOOR_STATE")
            {
                if (cmd.Params.TryGetValue("door_state", out var ds) && ds != null)
                {
                    _doorState = ds.ToString()?.ToUpperInvariant() ?? _doorState;
                    stateChanged = true;
                }
            }
            else if (cmd.Command == "TRIGGER_ALARM")
            {
                _tamperAlert = true;
                stateChanged = true;
            }
            else if (cmd.Command == "CLEAR_ALARM")
            {
                _tamperAlert = false;
                stateChanged = true;
            }

            if (stateChanged)
            {
                Console.WriteLine($"[Device {DeviceId}] 🚪 PHẢN HỒI LỆNH: Cửa hiện đang [{_doorState}] | Cảnh báo: {_tamperAlert}");
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
