using UDM_21.Shared;

namespace UDM_21.Tests;

public sealed class MqttConfigurationTests
{
    [Fact]
    public void TopicFactoryUsesIsolatedRootForAllMessageKinds()
    {
        Assert.Equal(
            "udm21_nhom01/lab/sensor/temp_hum_01/telemetry",
            MqttTopics.Telemetry(MqttTopics.DefaultRoot, "lab", "sensor", "temp_hum_01"));
        Assert.Equal("udm21_nhom01/+/+/+/status", MqttTopics.StatusWildcard(MqttTopics.DefaultRoot));
        Assert.Equal(
            "udm21_nhom01/home/light/smart_light_01/cmd",
            MqttTopics.Command(MqttTopics.DefaultRoot, "home", "light", "smart_light_01"));
    }

    [Fact]
    public void TopicValidationAcceptsConfiguredRootAndRejectsAnotherRoot()
    {
        var valid = MessageValidator.ValidateTopic(
            "team_alpha/lab/sensor/temp_hum_01/telemetry",
            "telemetry",
            "temp_hum_01",
            "sensor",
            "lab",
            out var validError,
            "team_alpha");
        var invalid = MessageValidator.ValidateTopic(
            "public/lab/sensor/temp_hum_01/telemetry",
            "telemetry",
            "temp_hum_01",
            "sensor",
            "lab",
            out var invalidError,
            "team_alpha");

        Assert.True(valid, validError);
        Assert.False(invalid);
        Assert.Contains("team_alpha", invalidError);
    }

    [Fact]
    public void ConnectionSettingsRejectPasswordWithoutUsername()
    {
        var settings = new MqttConnectionSettings
        {
            Host = "localhost",
            Port = 1883,
            Password = "not-logged-or-stored"
        };

        Assert.Throws<ArgumentException>(() => settings.Validate());
    }
}
