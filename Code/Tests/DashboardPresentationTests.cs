using System.Collections.Generic;
using UDM_21.Dashboard.Models;
using UDM_21.Dashboard.Services;
using Xunit;

namespace UDM_21.Tests
{
    public class DashboardPresentationTests
    {
        [Theory]
        [InlineData("air_quality_01", "Cảm biến chất lượng không khí", "Môi trường không khí")]
        [InlineData("door_sensor_01", "Cảm biến cửa", "An ninh ra vào")]
        [InlineData("power_meter_01", "Công tơ điện", "Đo lường năng lượng")]
        [InlineData("smart_light_01", "Đèn thông minh", "Hệ thống chiếu sáng")]
        [InlineData("temp_hum_01", "Nhiệt độ & độ ẩm", "Môi trường vi khí hậu")]
        public void DeviceDisplayNameResolverReturnsCorrectFriendlyNamesAndCategories(string id, string expectedName, string expectedCategory)
        {
            var name = DeviceDisplayNameResolver.GetFriendlyName(id);
            var category = DeviceDisplayNameResolver.GetCategoryName(id);

            Assert.Equal(expectedName, name);
            Assert.Equal(expectedCategory, category);
        }

        [Fact]
        public void DeviceItemFormatsLightTelemetryCorrectly()
        {
            var item = new DeviceItem { DeviceId = "smart_light_01" };
            item.UpdateTelemetryData(new Dictionary<string, object>
            {
                ["state"] = "ON",
                ["brightness"] = 80,
                ["power_draw_w"] = 10.5
            });

            Assert.Equal("BẬT", item.PrimaryValue);
            Assert.True(item.IsLightOn);
            Assert.Equal(80, item.Brightness);
            Assert.Contains("80%", item.SecondaryValue);
            Assert.Equal("LIGHT", item.VisualCategory);
        }

        [Fact]
        public void DeviceItemFormatsDoorSensorTelemetryCorrectly()
        {
            var item = new DeviceItem { DeviceId = "door_sensor_01" };
            item.UpdateTelemetryData(new Dictionary<string, object>
            {
                ["door_state"] = "OPEN",
                ["battery_pct"] = 95,
                ["tamper_alert"] = false
            });

            Assert.Equal("ĐANG MỞ", item.PrimaryValue);
            Assert.True(item.IsDoorOpen);
            Assert.Equal(95, item.BatteryPct);
            Assert.False(item.TamperAlert);
            Assert.Equal("DOOR", item.VisualCategory);
        }

        [Fact]
        public void DeviceItemFormatsPowerMeterTelemetryCorrectly()
        {
            var item = new DeviceItem { DeviceId = "power_meter_01" };
            item.UpdateTelemetryData(new Dictionary<string, object>
            {
                ["voltage_v"] = 220.5,
                ["current_a"] = 4.5,
                ["power_watt"] = 992.25,
                ["total_kwh"] = 125.4
            });

            Assert.Equal("992.2", item.PrimaryValue);
            Assert.Equal("W", item.PrimaryUnit);
            Assert.Equal(992.25, item.PowerWatt);
            Assert.Equal(125.4, item.TotalKwh);
            Assert.Equal("METER", item.VisualCategory);
        }

        [Fact]
        public void DeviceItemFormatsAirQualityTelemetryCorrectly()
        {
            var item = new DeviceItem { DeviceId = "air_quality_01" };
            item.UpdateTelemetryData(new Dictionary<string, object>
            {
                ["aqi"] = 45.0,
                ["co2_ppm"] = 410.0,
                ["air_status"] = "GOOD"
            });

            Assert.Equal("45", item.PrimaryValue);
            Assert.Equal("AQI", item.PrimaryUnit);
            Assert.Equal(45.0, item.Aqi);
            Assert.Contains("410", item.SecondaryValue);
            Assert.Equal("AIR", item.VisualCategory);
        }

        [Fact]
        public void DeviceItemFormatsClimateTelemetryCorrectly()
        {
            var item = new DeviceItem { DeviceId = "temp_hum_01" };
            item.UpdateTelemetryData(new Dictionary<string, object>
            {
                ["temperature"] = 26.8,
                ["humidity"] = 62.5
            });

            Assert.Equal("26.8", item.PrimaryValue);
            Assert.Equal("°C", item.PrimaryUnit);
            Assert.Equal("62.5%", item.SecondaryValue);
            Assert.Equal(26.8, item.Temperature);
            Assert.Equal(62.5, item.Humidity);
            Assert.Equal("CLIMATE", item.VisualCategory);
        }
    }
}
