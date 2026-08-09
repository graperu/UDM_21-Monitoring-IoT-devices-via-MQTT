using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UDM_21.Shared;

namespace UDM_21.Simulators.Devices
{
    public class SmartLightDevice : DeviceBase
    {
        private string _state = "OFF";
        private int _brightness = 80;

        public SmartLightDevice(string deviceId, string location, int interval = 5)
            : base(deviceId, "light", location, interval) { }

        protected override Dictionary<string, object> GenerateTelemetry()
        {
            return new Dictionary<string, object>
            {
                { "state", _state },
                { "brightness", _state == "ON" ? _brightness : 0 },
                { "power_draw_w", _state == "ON" ? 12.0 : 0.5 }
            };
        }

        protected override async Task HandleCommandAsync(CommandMessage cmd)
        {
            await base.HandleCommandAsync(cmd);
            bool stateChanged = false;

            if (cmd.Command == "TOGGLE_POWER")
            {
                if (cmd.Params.TryGetValue("state", out var stateObj) && stateObj != null)
                {
                    _state = stateObj.ToString()?.ToUpper() ?? (_state == "ON" ? "OFF" : "ON");
                }
                else
                {
                    _state = _state == "ON" ? "OFF" : "ON";
                }
                stateChanged = true;
            }
            else if (cmd.Command == "SET_BRIGHTNESS" && cmd.Params.TryGetValue("brightness", out var bObj) && bObj != null)
            {
                if (int.TryParse(bObj.ToString(), out int newB))
                {
                    _brightness = Math.Clamp(newB, 0, 100);
                    _state = _brightness > 0 ? "ON" : "OFF";
                    stateChanged = true;
                }
            }

            if (stateChanged)
            {
                Console.WriteLine($"[Device {DeviceId}] 💡 PHẢN HỒI LỆNH: Đèn đã thực sự {(_state == "ON" ? "BẬT" : "TẮT")} (Độ sáng: {(_state == "ON" ? _brightness : 0)}%)!");

                // Ngay lập tức gửi Telemetry mới để Dashboard phản hồi tức thì
                var data = GenerateTelemetry();
                var msg = new TelemetryMessage
                {
                    DeviceId = DeviceId,
                    DeviceType = DeviceType,
                    Location = Location,
                    Data = data
                };
                await Mqtt.PublishAsync(TelemetryTopic, msg.ToJson(), MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce);
            }
        }
    }
}
