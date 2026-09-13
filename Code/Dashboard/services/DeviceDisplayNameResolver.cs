namespace UDM_21.Dashboard.Services
{
    public static class DeviceDisplayNameResolver
    {
        public static string GetFriendlyName(string deviceId) => deviceId switch
        {
            "air_quality_01" => "Cảm biến chất lượng không khí",
            "door_sensor_01" => "Cảm biến cửa",
            "power_meter_01" => "Công tơ điện",
            "smart_light_01" => "Đèn thông minh",
            "temp_hum_01" => "Nhiệt độ & độ ẩm",
            _ => string.IsNullOrWhiteSpace(deviceId) ? "Thiết bị IoT" : deviceId
        };

        public static string GetCategoryName(string deviceId) => deviceId switch
        {
            "air_quality_01" => "Môi trường không khí",
            "door_sensor_01" => "An ninh ra vào",
            "power_meter_01" => "Đo lường năng lượng",
            "smart_light_01" => "Hệ thống chiếu sáng",
            "temp_hum_01" => "Môi trường vi khí hậu",
            _ => "Thiết bị IoT"
        };

        public static string GetLocationFriendly(string location) => location?.Trim().ToLowerInvariant() switch
        {
            "factory" => "Xưởng sản xuất",
            "lab" => "Phòng Lab",
            "home" => "Nhà",
            "entrance" => "Cửa chính",
            "main_panel" => "Tủ điện",
            "living_room" => "Phòng khách",
            "server_room" => "Phòng máy chủ",
            _ => string.IsNullOrWhiteSpace(location) ? "Chưa xác định" : location
        };
    }
}
