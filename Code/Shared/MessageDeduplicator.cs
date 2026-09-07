using System;
using System.Collections.Generic;

namespace UDM_21.Shared
{
    public sealed class MessageDeduplicator
    {
        private readonly int _capacity;
        private readonly HashSet<string> _messageIds = new HashSet<string>();
        private readonly Queue<string> _messageOrder = new Queue<string>();
        private readonly object _sync = new object();

        public MessageDeduplicator(int capacity = 100)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            _capacity = capacity;
        }

        public bool TryAccept(string messageId)
        {
            if (string.IsNullOrWhiteSpace(messageId)) return false;

            lock (_sync)
            {
                if (!_messageIds.Add(messageId)) return false;

                _messageOrder.Enqueue(messageId);
                while (_messageOrder.Count > _capacity)
                {
                    _messageIds.Remove(_messageOrder.Dequeue());
                }

                return true;
            }
        }
    }
}
