using System;

namespace UDM_21.Shared
{
    public sealed class MqttConnectionSettings
    {
        public string Host { get; init; } = "localhost";
        public int Port { get; init; } = 1883;
        public bool UseTls { get; init; }
        public string? Username { get; init; }
        public string? Password { get; init; }

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(Host))
                throw new ArgumentException("Broker host không được để trống.", nameof(Host));
            if (Port is < 1 or > 65535)
                throw new ArgumentOutOfRangeException(nameof(Port), "Port phải nằm trong khoảng 1..65535.");
            if (string.IsNullOrWhiteSpace(Username) && !string.IsNullOrEmpty(Password))
                throw new ArgumentException("Phải nhập username khi sử dụng password.", nameof(Username));
        }
    }
}
