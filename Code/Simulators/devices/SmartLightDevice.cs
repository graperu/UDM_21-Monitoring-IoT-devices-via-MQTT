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

        protected override Task HandleCommandAsync(CommandMessage cmd)
        {
            base.HandleCommandAsync(cmd);
            if (cmd.Command == "TOGGLE_POWER")
            {
                if (cmd.Params.TryGetValue("state", out var stateObj))
                {
                    _state = stateObj?.ToString() ?? "OFF";
                }
            }
            return Task.CompletedTask;
        }
    }
}
