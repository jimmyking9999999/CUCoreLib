using System;
using System.Collections.Generic;

namespace CUCoreLib.Registries.Infrastructure
{
    internal sealed class RegistrationOwnershipIndex<TKey>
    {
        private readonly Dictionary<TKey, string> ownerByKey;
        private readonly Dictionary<string, HashSet<TKey>> keysByOwner;
        private string activeOwnerId;

        internal RegistrationOwnershipIndex(IEqualityComparer<TKey> keyComparer = null)
        {
            var comparer = keyComparer ?? EqualityComparer<TKey>.Default;
            ownerByKey = new Dictionary<TKey, string>(comparer);
            keysByOwner = new Dictionary<string, HashSet<TKey>>(StringComparer.OrdinalIgnoreCase);
            KeyComparer = comparer;
        }

        internal IEqualityComparer<TKey> KeyComparer { get; }

        internal IDisposable BeginScope(string ownerId)
        {
            return new OwnerScope(this, NormalizeOwnerId(ownerId));
        }

        internal void Assign(TKey key, string ambientOwnerId)
        {
            var ownerId = activeOwnerId ?? NormalizeOwnerId(ambientOwnerId);
            if (ownerId == null) return;

            if (ownerByKey.TryGetValue(key, out var previousOwnerId))
            {
                if (string.Equals(previousOwnerId, ownerId, StringComparison.OrdinalIgnoreCase)) return;

                RemoveFromOwner(previousOwnerId, key);
            }

            ownerByKey[key] = ownerId;
            if (!keysByOwner.TryGetValue(ownerId, out var keys))
            {
                keys = new HashSet<TKey>(KeyComparer);
                keysByOwner[ownerId] = keys;
            }

            keys.Add(key);
        }

        internal TKey[] GetKeys(string ownerId)
        {
            var normalizedOwnerId = NormalizeOwnerId(ownerId);
            if (normalizedOwnerId == null || !keysByOwner.TryGetValue(normalizedOwnerId, out var keys))
                return Array.Empty<TKey>();

            var snapshot = new TKey[keys.Count];
            keys.CopyTo(snapshot);
            return snapshot;
        }

        internal bool IsOwnedBy(TKey key, string ownerId)
        {
            var normalizedOwnerId = NormalizeOwnerId(ownerId);
            return normalizedOwnerId != null && ownerByKey.TryGetValue(key, out var registeredOwnerId) &&
                   string.Equals(registeredOwnerId, normalizedOwnerId, StringComparison.OrdinalIgnoreCase);
        }

        internal void Remove(TKey key)
        {
            if (!ownerByKey.TryGetValue(key, out var ownerId)) return;

            ownerByKey.Remove(key);
            RemoveFromOwner(ownerId, key);
        }

        private void RemoveFromOwner(string ownerId, TKey key)
        {
            if (!keysByOwner.TryGetValue(ownerId, out var keys)) return;

            keys.Remove(key);
            if (keys.Count == 0) keysByOwner.Remove(ownerId);
        }

        private static string NormalizeOwnerId(string ownerId)
        {
            return string.IsNullOrWhiteSpace(ownerId) ? null : ownerId.Trim();
        }

        private sealed class OwnerScope : IDisposable
        {
            private readonly RegistrationOwnershipIndex<TKey> index;
            private readonly string previousOwnerId;
            private bool disposed;

            internal OwnerScope(RegistrationOwnershipIndex<TKey> index, string ownerId)
            {
                this.index = index;
                previousOwnerId = index.activeOwnerId;
                index.activeOwnerId = ownerId;
            }

            public void Dispose()
            {
                if (disposed) return;

                index.activeOwnerId = previousOwnerId;
                disposed = true;
            }
        }
    }
}
