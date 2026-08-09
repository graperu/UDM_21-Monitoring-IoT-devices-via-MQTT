using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UDM_21.Simulators.Devices;

namespace UDM_21.Simulators
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("==============================================");
            Console.WriteLine(" UDM_21: IoT Devices Simulator (.NET 8)");
            Console.WriteLine("==============================================");

            string host = args.Length > 0 ? args[0] : "localhost";
            int port = args.Length > 1 && int.TryParse(args[1], out int p) ? p : 1883;

            var devices = new List<DeviceBase>
            {
                new TempHumidityDevice("temp_hum_01", "lab", interval: 3),
                new AirQualityDevice("air_quality_01", "factory", interval: 4),
                new PowerMeterDevice("power_meter_01", "home", interval: 2),
                new SmartLightDevice("smart_light_01", "home", interval: 5),
                new DoorSensorDevice("door_sensor_01", "lab", interval: 5)
            };

            Console.WriteLine($"Connecting 5 devices to Broker {host}:{port}...");

            foreach (var dev in devices)
            {
                await dev.StartAsync(host, port);
            }

            Console.WriteLine("All 5 IoT Simulators are running. Press Enter to exit.");
            Console.ReadLine();

            Console.WriteLine("Stopping all devices...");
            foreach (var dev in devices)
            {
                await dev.StopAsync();
            }

            Console.WriteLine("Simulators stopped cleanly.");
        }
    }
}
