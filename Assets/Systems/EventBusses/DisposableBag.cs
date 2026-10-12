using System;
using System.Collections.Generic;

namespace Systems.EventBusses {
    /// <summary>Collects bus subscriptions so they can be released in one call (OnDestroy / Dispose).</summary>
    public sealed class DisposableBag : IDisposable {
        private readonly List<IDisposable> _items = new();

        public void Add(IDisposable item) {
            if (item != null) _items.Add(item);
        }

        public void Dispose() {
            foreach (IDisposable item in _items) item.Dispose();
            _items.Clear();
        }
    }
}
