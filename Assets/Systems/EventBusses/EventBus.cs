using System;
using System.Collections.Generic;

namespace Systems.EventBusses {
    public interface IEvent { }

    public interface IEventBus {
        void Publish<T>(in T evt) where T : struct, IEvent;
        IDisposable Subscribe<T>(Action<T> handler) where T : struct, IEvent;
    }

    public sealed class EventBus : IEventBus {
        private readonly Dictionary<Type, Delegate> _handlers = new();

        public void Publish<T>(in T evt) where T : struct, IEvent {
            if (_handlers.TryGetValue(typeof(T), out var d))
                ((Action<T>)d)?.Invoke(evt);
        }

        public IDisposable Subscribe<T>(Action<T> handler) where T : struct, IEvent {
            _handlers[typeof(T)] = _handlers.TryGetValue(typeof(T), out var d)
                ? Delegate.Combine(d, handler)
                : handler;
            return new Subscription<T>(this, handler);
        }

        private void Unsubscribe<T>(Action<T> handler) where T : struct, IEvent {
            if (!_handlers.TryGetValue(typeof(T), out var d)) return;
            var result = Delegate.Remove(d, handler);
            if (result == null) _handlers.Remove(typeof(T));
            else _handlers[typeof(T)] = result;
        }

        private sealed class Subscription<T> : IDisposable where T : struct, IEvent {
            private readonly EventBus _bus;
            private readonly Action<T> _handler;
            private bool _disposed;

            public Subscription(EventBus bus, Action<T> handler) {
                _bus = bus;
                _handler = handler;
            }

            public void Dispose() {
                if (_disposed) return;
                _disposed = true;
                _bus.Unsubscribe(_handler);
            }
        }
    }
}
