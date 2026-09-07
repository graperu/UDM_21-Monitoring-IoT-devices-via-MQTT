using System;
using System.Diagnostics;
using System.IO;

namespace UDM_21.Shared
{
    public static class AppLogger
    {
        private static readonly object Sync = new object();
        private static readonly string ProcessName =
            Path.GetFileNameWithoutExtension(Environment.ProcessPath) ?? "UDM21";
        private static readonly string LogDirectory = ResolveLogDirectory();

        public static string CurrentLogFile => Path.Combine(
            LogDirectory,
            $"udm21-{ProcessName}-{Environment.ProcessId}-{DateTime.Now:yyyyMMdd}.log");

        public static void Info(string eventName, string message) => Write("INFO", eventName, message);
        public static void Warning(string eventName, string message) => Write("WARN", eventName, message);
        public static void Error(string eventName, Exception exception) =>
            Write("ERROR", eventName, $"{exception.GetType().Name}: {exception.Message}");

        private static void Write(string level, string eventName, string message)
        {
            try
            {
                var safeEvent = Sanitize(eventName);
                var safeMessage = Sanitize(message);
                var line = $"{DateTimeOffset.Now:O}\t{level}\t{safeEvent}\t{safeMessage}{Environment.NewLine}";

                lock (Sync)
                {
                    Directory.CreateDirectory(LogDirectory);
                    File.AppendAllText(CurrentLogFile, line);
                }
            }
            catch (IOException)
            {
                Debug.WriteLine("Không thể ghi file log UDM_21.");
            }
            catch (UnauthorizedAccessException)
            {
                Debug.WriteLine("Không có quyền ghi file log UDM_21.");
            }
        }

        private static string ResolveLogDirectory()
        {
            var configured = Environment.GetEnvironmentVariable("UDM21_LOG_DIR");
            if (!string.IsNullOrWhiteSpace(configured)) return Path.GetFullPath(configured);

            return Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, "Extra", "logs"));
        }

        private static string Sanitize(string value) =>
            value.Replace("\r", " ").Replace("\n", " ").Replace("\t", " ");
    }
}
