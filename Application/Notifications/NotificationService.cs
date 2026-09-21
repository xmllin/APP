using System;
using System.Collections.Generic;
using WpfApp1.Services;

namespace WpfApp1.Application.Notifications
{
    public sealed class NotificationService
    {
        private readonly Queue<NotificationEntry> _queue = new Queue<NotificationEntry>();
        private readonly HashSet<string> _keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public int Count => _queue.Count;

        public void Enqueue(string key, string message, NotificationKind kind)
        {
            key = string.IsNullOrWhiteSpace(key) ? Guid.NewGuid().ToString("N") : key;
            if (!_keys.Add(key)) return;
            _queue.Enqueue(new NotificationEntry { Key = key, Message = message, Kind = kind });
        }

        public bool TryDequeue(out NotificationEntry entry)
        {
            if (_queue.Count == 0) { entry = null; return false; }
            entry = _queue.Dequeue();
            _keys.Remove(entry.Key);
            return true;
        }

        public sealed class NotificationEntry
        {
            public string Key { get; set; }
            public string Message { get; set; }
            public NotificationKind Kind { get; set; }
        }
    }
}
