using System;
using System.Collections.Generic;

namespace UDM_21.Shared
{
    public sealed class TimestampOrderingFilter
    {
        private readonly Dictionary<string, DateTimeOffset> _latestByKey =
            new Dictionary<string, DateTimeOffset>();
        private readonly object _sync = new object();

        public bool TryAccept(string key, string timestampValue, out string rejectionReason)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                rejectionReason = "Ordering key không hợp lệ.";
                return false;
            }

            if (!MessageValidator.TryParseUtcTimestamp(timestampValue, out var timestamp))
            {
                rejectionReason = "Timestamp không hợp lệ.";
                return false;
            }

            lock (_sync)
            {
                if (_latestByKey.TryGetValue(key, out var latest) && timestamp < latest)
                {
                    rejectionReason = $"Out-of-order message: {key} ({timestamp:O} < {latest:O})";
                    return false;
                }

                _latestByKey[key] = timestamp;
            }

            rejectionReason = string.Empty;
            return true;
        }
    }
}
