using System;
using System.Collections.Generic;

namespace UDM_21.Shared
{
    // One terminal offline state per connection; a reconnect starts a new epoch.
    public sealed class DeviceStatusFilter
    {
        private sealed record State(DateTimeOffset? Epoch, DateTimeOffset Timestamp, bool Offline);
        private readonly Dictionary<string, State> _states = new();
        private readonly MessageDeduplicator _duplicates = new(200);
        private readonly object _sync = new();

        public bool TryAccept(DeviceStatusMessage message, out string reason)
        {
            if (!MessageValidator.ValidateStatus(message, out reason)) return false;
            MessageValidator.TryParseUtcTimestamp(message.Timestamp, out var timestamp);
            DateTimeOffset? epoch = null;
            if (message.ConnectionStartedAt != null)
            {
                MessageValidator.TryParseUtcTimestamp(message.ConnectionStartedAt, out var parsed);
                epoch = parsed;
            }
            var offline = string.Equals(message.Status, "offline", StringComparison.OrdinalIgnoreCase);

            lock (_sync)
            {
                if (_states.TryGetValue(message.DeviceId, out var last))
                {
                    if (last.Epoch.HasValue && (!epoch.HasValue || epoch.Value < last.Epoch.Value))
                    {
                        reason = "Status belongs to an older connection.";
                        return false;
                    }
                    if (epoch == last.Epoch)
                    {
                        if (epoch.HasValue && last.Offline && !offline)
                        {
                            reason = "Connection already offline; delayed online status rejected.";
                            return false;
                        }
                        // A Will's timestamp is the CONNECT time, not the disconnect time.
                        if (!message.IsWill && timestamp < last.Timestamp)
                        {
                            reason = "Out-of-order status message.";
                            return false;
                        }
                    }
                }

                if (!_duplicates.TryAccept(message.DeviceId + ":" + Guid.Parse(message.MessageId).ToString("N")))
                {
                    reason = "Duplicate status message.";
                    return false;
                }
                _states[message.DeviceId] = new State(epoch, timestamp, offline);
            }
            reason = string.Empty;
            return true;
        }
    }
}
