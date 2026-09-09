using System;
using System.Collections.Generic;

namespace UDM_21.Shared
{
    public sealed class TelemetryMessageFilter
    {
        private readonly int _maxCacheSize;
        private readonly HashSet<string> _processedMessageIds = new HashSet<string>();
        private readonly Queue<string> _messageOrder = new Queue<string>();
        private readonly Dictionary<string, DateTimeOffset> _lastTimestampByDevice =
            new Dictionary<string, DateTimeOffset>();
        private readonly object _sync = new object();

        public TelemetryMessageFilter(int maxCacheSize = 100)
        {
            if (maxCacheSize <= 0) throw new ArgumentOutOfRangeException(nameof(maxCacheSize));
            _maxCacheSize = maxCacheSize;
        }

        public bool TryAccept(TelemetryMessage message, out string rejectionReason)
        {
            if (!MessageValidator.ValidateTelemetry(message, out rejectionReason)) return false;
            MessageValidator.TryParseUtcTimestamp(message.Timestamp, out var timestamp);

            lock (_sync)
            {
                if (_processedMessageIds.Contains(message.MessageId))
                {
                    rejectionReason = $"Duplicate message: {message.MessageId}";
                    return false;
                }

                if (_lastTimestampByDevice.TryGetValue(message.DeviceId, out var lastTimestamp) &&
                    timestamp < lastTimestamp)
                {
                    rejectionReason =
                        $"Out-of-order message: {message.DeviceId} ({timestamp:O} < {lastTimestamp:O})";
                    return false;
                }

                _processedMessageIds.Add(message.MessageId);
                _messageOrder.Enqueue(message.MessageId);
                _lastTimestampByDevice[message.DeviceId] = timestamp;

                while (_messageOrder.Count > _maxCacheSize)
                {
                    _processedMessageIds.Remove(_messageOrder.Dequeue());
                }
            }

            rejectionReason = string.Empty;
            return true;
        }
    }
}
