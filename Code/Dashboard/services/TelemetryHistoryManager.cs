using System.Collections.Generic;
using System.Linq;
using UDM_21.Shared;

namespace UDM_21.Dashboard.Services
{
    public class TelemetryHistoryManager
    {
        private const int MaxHistory = 20;

        private readonly Dictionary<string, Queue<TelemetryMessage>> _history =
            new Dictionary<string, Queue<TelemetryMessage>>();

        private readonly object _lock = new object();

        public void AddTelemetry(TelemetryMessage message)
        {
            if (message == null || string.IsNullOrWhiteSpace(message.DeviceId))
                return;

            lock (_lock)
            {
                if (!_history.ContainsKey(message.DeviceId))
                {
                    _history[message.DeviceId] =
                        new Queue<TelemetryMessage>();
                }

                var queue = _history[message.DeviceId];

                queue.Enqueue(message);

                while (queue.Count > MaxHistory)
                {
                    queue.Dequeue();
                }
            }
        }

        public List<TelemetryMessage> GetHistory(string deviceId)
        {
            if (string.IsNullOrWhiteSpace(deviceId))
                return new List<TelemetryMessage>();

            lock (_lock)
            {
                if (!_history.ContainsKey(deviceId))
                    return new List<TelemetryMessage>();

                return _history[deviceId].ToList();
            }
        }

        public int GetCount(string deviceId)
        {
            lock (_lock)
            {
                if (!_history.ContainsKey(deviceId))
                    return 0;

                return _history[deviceId].Count;
            }
        }

        public void ClearHistory(string deviceId)
        {
            if (string.IsNullOrWhiteSpace(deviceId))
                return;

            lock (_lock)
            {
                if (_history.ContainsKey(deviceId))
                {
                    _history[deviceId].Clear();
                }
            }
        }

        public void ClearAll()
        {
            lock (_lock)
            {
                _history.Clear();
            }
        }
    }
}