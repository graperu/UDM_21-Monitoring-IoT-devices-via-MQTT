namespace UDM_21.Dashboard.Models
{
    public class DeviceMetricTile
    {
        public string Label { get; set; } = string.Empty;
        public string Value { get; set; } = "--";
        public string Unit { get; set; } = string.Empty;
        public string Subtext { get; set; } = string.Empty;
        public string BadgeColor { get; set; } = "#4F46E5";
        public bool IsAlert { get; set; }

        public DeviceMetricTile() { }

        public DeviceMetricTile(string label, string value, string unit, string subtext = "", string badgeColor = "#4F46E5", bool isAlert = false)
        {
            Label = label;
            Value = value;
            Unit = unit;
            Subtext = subtext;
            BadgeColor = badgeColor;
            IsAlert = isAlert;
        }
    }
}
