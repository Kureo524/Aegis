using System.Collections.Generic;
using NUnit.Framework;
using Systems.EventBusses;

namespace Game.Tests.EditMode {
    public class EventBusTests {
        private readonly struct PingEvent : IEvent {
            public readonly int Value;
            public PingEvent(int value) => Value = value;
        }

        private readonly struct OtherEvent : IEvent { }

        [Test]
        public void Publish_ReachesEverySubscriberOfThatType() {
            var bus = new EventBus();
            var received = new List<int>();
            bus.Subscribe<PingEvent>(e => received.Add(e.Value));
            bus.Subscribe<PingEvent>(e => received.Add(e.Value * 10));

            bus.Publish(new PingEvent(3));

            CollectionAssert.AreEqual(new[] { 3, 30 }, received);
        }

        [Test]
        public void Publish_DoesNotReachOtherEventTypes() {
            var bus = new EventBus();
            int calls = 0;
            bus.Subscribe<OtherEvent>(_ => calls++);

            bus.Publish(new PingEvent(1));

            Assert.AreEqual(0, calls);
        }

        [Test]
        public void Publish_WithoutSubscribers_DoesNothing() {
            Assert.DoesNotThrow(() => new EventBus().Publish(new PingEvent(1)));
        }

        [Test]
        public void DisposingSubscription_StopsDelivery_AndIsIdempotent() {
            var bus = new EventBus();
            int calls = 0;
            var sub = bus.Subscribe<PingEvent>(_ => calls++);

            sub.Dispose();
            sub.Dispose();
            bus.Publish(new PingEvent(1));

            Assert.AreEqual(0, calls);
        }

        [Test]
        public void DisposableBag_DisposesEverySubscription() {
            var bus = new EventBus();
            int calls = 0;
            var bag = new DisposableBag();
            bag.Add(bus.Subscribe<PingEvent>(_ => calls++));
            bag.Add(bus.Subscribe<PingEvent>(_ => calls++));

            bag.Dispose();
            bus.Publish(new PingEvent(1));

            Assert.AreEqual(0, calls);
        }
    }
}
