using System;
using System.Collections.Generic;
using UDM_21.Shared;

namespace UDM_21.Simulators
{
    public sealed class SimulatorOptions
    {
        public string Host { get; private set; } =
            Environment.GetEnvironmentVariable("UDM21_MQTT_HOST") ?? "broker.emqx.io";
        public int Port { get; private set; } = ReadPortFromEnvironment();
        public bool UseTls { get; private set; } = ReadBooleanEnvironment("UDM21_MQTT_TLS");
        public string TopicRoot { get; private set; } =
            Environment.GetEnvironmentVariable("UDM21_TOPIC_ROOT") ?? MqttTopics.DefaultRoot;
        public string Device { get; private set; } = "all";
        public string? Username { get; private set; } = Environment.GetEnvironmentVariable("UDM21_MQTT_USERNAME");
        public string? Password { get; private set; } = Environment.GetEnvironmentVariable("UDM21_MQTT_PASSWORD");
        public bool ShowHelp { get; private set; }

        public MqttConnectionSettings Connection => new()
        {
            Host = Host,
            Port = Port,
            UseTls = UseTls,
            Username = Username,
            Password = Password
        };

        public static SimulatorOptions Parse(string[] args)
        {
            var options = new SimulatorOptions();
            var positionalIndex = 0;

            for (var index = 0; index < args.Length; index++)
            {
                var argument = args[index];
                switch (argument)
                {
                    case "--help":
                    case "-h":
                        options.ShowHelp = true;
                        break;
                    case "--host":
                        options.Host = NextValue(args, ref index, argument);
                        break;
                    case "--port":
                        options.Port = ParsePort(NextValue(args, ref index, argument));
                        break;
                    case "--topic-root":
                        options.TopicRoot = NextValue(args, ref index, argument);
                        break;
                    case "--device":
                        options.Device = NextValue(args, ref index, argument);
                        break;
                    case "--tls":
                        options.UseTls = true;
                        break;
                    case "--no-tls":
                        options.UseTls = false;
                        break;
                    case "--username":
                        options.Username = NextValue(args, ref index, argument);
                        break;
                    case "--password-env":
                        var variableName = NextValue(args, ref index, argument);
                        options.Password = Environment.GetEnvironmentVariable(variableName)
                            ?? throw new ArgumentException($"Environment variable '{variableName}' chưa được đặt.");
                        break;
                    default:
                        // Preserve the old syntax: Simulators.exe <host> <port>.
                        if (argument.StartsWith('-'))
                            throw new ArgumentException($"Tham số không được hỗ trợ: {argument}");
                        if (positionalIndex == 0)
                            options.Host = argument;
                        else if (positionalIndex == 1)
                            options.Port = ParsePort(argument);
                        else
                            throw new ArgumentException($"Tham số dư: {argument}");
                        positionalIndex++;
                        break;
                }
            }

            options.Host = options.Host.Trim();
            options.TopicRoot = MqttTopics.NormalizeRoot(options.TopicRoot);
            options.Device = options.Device.Trim().ToLowerInvariant();
            options.Connection.Validate();
            return options;
        }

        public static IReadOnlyCollection<string> SupportedDevices { get; } = new[]
        {
            "all",
            "temp_hum_01",
            "air_quality_01",
            "power_meter_01",
            "smart_light_01",
            "door_sensor_01"
        };

        private static string NextValue(string[] args, ref int index, string argument)
        {
            if (++index >= args.Length || string.IsNullOrWhiteSpace(args[index]))
                throw new ArgumentException($"Thiếu giá trị cho {argument}.");
            return args[index];
        }

        private static int ParsePort(string value)
        {
            if (!int.TryParse(value, out var port) || port is < 1 or > 65535)
                throw new ArgumentException("Port phải là số trong khoảng 1..65535.");
            return port;
        }

        private static int ReadPortFromEnvironment()
        {
            var value = Environment.GetEnvironmentVariable("UDM21_MQTT_PORT");
            return string.IsNullOrWhiteSpace(value) ? 1883 : ParsePort(value);
        }

        private static bool ReadBooleanEnvironment(string name)
        {
            var value = Environment.GetEnvironmentVariable(name);
            return string.Equals(value, "1", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);
        }
    }
}
