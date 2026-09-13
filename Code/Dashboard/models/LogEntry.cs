using System;

namespace UDM_21.Dashboard.Models
{
    public class LogEntry
    {
        public string Time { get; set; } = DateTime.Now.ToString("HH:mm:ss");
        public string Category { get; set; } = "INFO";
        public string Message { get; set; } = string.Empty;
        public string CategoryBg { get; set; } = "#E2E8F0";
        public string CategoryFg { get; set; } = "#475569";

        public LogEntry() { }

        public LogEntry(string category, string message)
        {
            Time = DateTime.Now.ToString("HH:mm:ss");
            Category = category.ToUpperInvariant();
            Message = message;

            switch (Category)
            {
                case "INFO":
                    CategoryBg = "#E0F2FE"; // Sky light
                    CategoryFg = "#0369A1"; // Sky dark
                    break;
                case "MQTT":
                    CategoryBg = "#EDE9FE"; // Purple light
                    CategoryFg = "#6D28D9"; // Purple dark
                    break;
                case "CMD":
                case "COMMAND":
                    CategoryBg = "#FEF3C7"; // Amber light
                    CategoryFg = "#B45309"; // Amber dark
                    break;
                case "WARN":
                case "WARNING":
                    CategoryBg = "#FFEDD5"; // Orange light
                    CategoryFg = "#C2410C"; // Orange dark
                    break;
                case "ERROR":
                case "ALERT":
                    CategoryBg = "#FEE2E2"; // Red light
                    CategoryFg = "#B91C1C"; // Red dark
                    break;
                case "SUCCESS":
                    CategoryBg = "#DCFCE7"; // Green light
                    CategoryFg = "#15803D"; // Green dark
                    break;
                default:
                    CategoryBg = "#F1F5F9";
                    CategoryFg = "#475569";
                    break;
            }
        }
    }
}
