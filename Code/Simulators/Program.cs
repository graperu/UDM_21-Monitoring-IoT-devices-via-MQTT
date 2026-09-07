using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UDM_21.Simulators.Devices;
using UDM_21.Shared;

namespace UDM_21.Simulators
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("==============================================");
            Console.WriteLine(" UDM_21: IoT Devices Simulator (.NET 8)");
            Console.WriteLine("==============================================");

            string host = args.Length > 0 ? args[0] : "broker.emqx.io";
            int port = args.Length > 1 && int.TryParse(args[1], out int p) ? p : 1883;

            if (args.Length == 0)
            {
                Console.WriteLine("[INFO] Khong co tham so Broker host. Mac dinh su dung Public Broker: broker.emqx.io:1883");
                Console.WriteLine("       (De dung local broker, chay: dotnet run --project Code/Simulators/Simulators.csproj localhost 1883)\n");
            }

            var devices = new List<DeviceBase>
            {
                new TempHumidityDevice("temp_hum_01", "lab", interval: 3),
                new AirQualityDevice("air_quality_01", "factory", interval: 4),
                new PowerMeterDevice("power_meter_01", "home", interval: 2),
                new SmartLightDevice("smart_light_01", "home", interval: 5),
                new DoorSensorDevice("door_sensor_01", "lab", interval: 5)
            };

            Console.WriteLine($"Connecting 5 devices to Broker {host}:{port}...");
            var exitRequested = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            Console.CancelKeyPress += (_, eventArgs) =>
            {
                eventArgs.Cancel = true;
                exitRequested.TrySetResult(true);
            };

            try
            {
                foreach (var dev in devices)
                {
                    await dev.StartAsync(host, port);
                }

                Console.WriteLine("All 5 IoT Simulators are running. Press Enter or Ctrl+C to exit.");
                _ = Task.Run(() =>
                {
                    Console.ReadLine();
                    exitRequested.TrySetResult(true);
                });
                await exitRequested.Task;
            }
            catch (Exception ex)
            {
                AppLogger.Error("SIMULATOR_FATAL", ex);
                Console.Error.WriteLine($"[FATAL] {ex.Message}");
            }
            finally
            {
                Console.WriteLine("Stopping all devices...");
                foreach (var dev in devices)
                {
                    try
                    {
                        await dev.DisposeAsync();
                    }
                    catch (Exception ex)
                    {
                        AppLogger.Error("DEVICE_SHUTDOWN_FAILED", ex);
                    }
                }

                Console.WriteLine("Simulators stopped cleanly.");
            }
        }
    }
}
