using Newtonsoft.Json;
using UDM_21.Shared;

namespace UDM_21.Tests;

public class MessageValidationTests
{
    [Fact]
    public void RejectsMalformedJson()
    {
        var accepted = MessageValidator.TryParseTelemetry("{not-json", out _, out var error);

        Assert.False(accepted);
        Assert.Contains("JSON", error);
    }

    [Fact]
    public void RejectsInvalidGuidAndTimestamp()
    {
        var message = ValidTelemetry();
        message.MessageId = "not-a-guid";
        message.Timestamp = "yesterday";

        var accepted = MessageValidator.ValidateTelemetry(message, out var error);

        Assert.False(accepted);
        Assert.Contains("GUID", error);
    }

    [Fact]
    public void RejectsPayloadWhoseIdentityDoesNotMatchTopic()
    {
        var message = ValidTelemetry();

        var accepted = MessageValidator.ValidateTopic(
            "udm21_nhom01/lab/sensor/another_device/telemetry",
            "telemetry",
            message.DeviceId,
            message.DeviceType,
            message.Location,
            out var error);

        Assert.False(accepted);
        Assert.Contains("không khớp", error);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void RejectsBrightnessOutsideAllowedRange(int brightness)
    {
        var parameters = new Dictionary<string, object> { ["brightness"] = brightness };

        var accepted = MessageValidator.TryValidateCommandForDevice(
            "light",
            "SET_BRIGHTNESS",
            parameters,
            out var error);

        Assert.False(accepted);
        Assert.Contains("0..100", error);
    }

    [Fact]
    public void AcceptsValidTelemetryJson()
    {
        var json = JsonConvert.SerializeObject(ValidTelemetry());

        var accepted = MessageValidator.TryParseTelemetry(json, out var message, out var error);

        Assert.True(accepted, error);
        Assert.Equal("temp_hum_01", message!.DeviceId);
    }

    private static TelemetryMessage ValidTelemetry() => new()
    {
        MessageId = Guid.NewGuid().ToString(),
        DeviceId = "temp_hum_01",
        DeviceType = "sensor",
        Location = "lab",
        Timestamp = DateTimeOffset.UtcNow.ToString("O"),
        Data = new Dictionary<string, object> { ["temperature"] = 25.5 }
    };
}
