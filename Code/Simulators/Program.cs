using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UDM_21.Simulators.Devices;
using UDM_21.Shared;

namespace UDM_21.Simulators
{
    internal static class Program
    {
        private static async Task Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.WriteLine("==============================================");
            Console.WriteLine(" UDM_21: IoT Devices Simulator (.NET 8)");
            Console.WriteLine("==============================================");

            SimulatorOptions options;
            try
            {
                options = SimulatorOptions.Parse(args);
            }
            catch (ArgumentException ex)
            {
                Console.Error.WriteLine($"[CONFIG ERROR] {ex.Message}");
                PrintHelp();
                Environment.ExitCode = 2;
                return;
            }

            if (options.ShowHelp)
            {
                PrintHelp();
                return;
            }

            var devices = CreateDevices(options.Device, options.TopicRoot);
            if (devices.Count == 0)
            {
                Console.Error.WriteLine(
                    $"[CONFIG ERROR] Device '{options.Device}' không tồn tại. Hỗ trợ: {string.Join(", ", SimulatorOptions.SupportedDevices)}");
                Environment.ExitCode = 2;
                return;
            }

            Console.WriteLine(
                $"Connecting {devices.Count} device(s) to {(options.UseTls ? "mqtts" : "mqtt")}://{options.Host}:{options.Port}");
            Console.WriteLine($"Topic root: {options.TopicRoot}");
            Console.WriteLine($"Mode: {(options.Device == "all" ? "all devices in one process" : $"single process for {options.Device}")}");

            var exitRequested = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            Console.CancelKeyPress += (_, eventArgs) =>
            {
                eventArgs.Cancel = true;
                exitRequested.TrySetResult(true);
            };

            try
            {
                foreach (var device in devices)
                {
                    await device.StartAsync(options.Connection);
                }

                Console.WriteLine("Simulator(s) running. Press Enter or Ctrl+C to exit.");
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
                Environment.ExitCode = 1;
            }
            finally
            {
                Console.WriteLine("Stopping simulator(s)...");
                foreach (var device in devices)
                {
                    try
                    {
                        await device.DisposeAsync();
                    }
                    catch (Exception ex)
                    {
                        AppLogger.Error("DEVICE_SHUTDOWN_FAILED", ex);
                    }
                }

                Console.WriteLine("Simulator(s) stopped cleanly.");
            }
        }

        private static List<DeviceBase> CreateDevices(string selection, string topicRoot)
        {
            var factories = new Dictionary<string, Func<DeviceBase>>(StringComparer.Ordinal)
            {
                ["temp_hum_01"] = () => new TempHumidityDevice("temp_hum_01", "lab", interval: 3, topicRoot: topicRoot),
                ["air_quality_01"] = () => new AirQualityDevice("air_quality_01", "factory", interval: 4, topicRoot: topicRoot),
                ["power_meter_01"] = () => new PowerMeterDevice("power_meter_01", "home", interval: 2, topicRoot: topicRoot),
                ["smart_light_01"] = () => new SmartLightDevice("smart_light_01", "home", interval: 5, topicRoot: topicRoot),
                ["door_sensor_01"] = () => new DoorSensorDevice("door_sensor_01", "lab", interval: 5, topicRoot: topicRoot)
            };

            if (selection == "all") return factories.Values.Select(factory => factory()).ToList();
            return factories.TryGetValue(selection, out var selected)
                ? new List<DeviceBase> { selected() }
                : new List<DeviceBase>();
        }

        private static void PrintHelp()
        {
            Console.WriteLine("Usage:");
            Console.WriteLine("  dotnet run --project Code/Simulators -- [options]");
            Console.WriteLine("Options:");
            Console.WriteLine("  --host <host>              MQTT broker host");
            Console.WriteLine("  --port <1..65535>          MQTT broker port");
            Console.WriteLine($"  --topic-root <name>        Default: {MqttTopics.DefaultRoot}");
            Console.WriteLine("  --device <id|all>          Run one device per process or all devices");
            Console.WriteLine("  --tls                      Enable TLS with normal certificate validation");
            Console.WriteLine("  --username <username>      MQTT username");
            Console.WriteLine("  --password-env <name>      Read password from an environment variable");
            Console.WriteLine("Environment: UDM21_MQTT_HOST, UDM21_MQTT_PORT, UDM21_MQTT_TLS,");
            Console.WriteLine("             UDM21_MQTT_USERNAME, UDM21_MQTT_PASSWORD, UDM21_TOPIC_ROOT");
        }
    }
}
