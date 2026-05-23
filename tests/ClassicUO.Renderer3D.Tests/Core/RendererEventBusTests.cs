// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Tests for RendererEventBus (Renderer3D Core, ADR-012).

using System;
using ClassicUO.Renderer.Core;
using FluentAssertions;
using Xunit;

namespace ClassicUO.Renderer3D.Tests.Core
{
    /// <summary>
    /// Locks the publish/subscribe contract that every event-driven service depends on:
    /// snapshot-on-publish, dispose-unsubscribes, type-keyed routing, multi-subscriber
    /// fan-out, and survival of in-flight subscribe/unsubscribe during dispatch.
    /// </summary>
    public sealed class RendererEventBusTests
    {
        // Test events — readonly structs per playbook's allocation-free dispatch rule.
        private readonly struct PingEvent
        {
            public readonly int Value;
            public PingEvent(int value) { Value = value; }
        }

        private readonly struct PongEvent
        {
            public readonly string Label;
            public PongEvent(string label) { Label = label; }
        }

        [Fact]
        public void Subscribe_RejectsNullHandler()
        {
            var bus = new RendererEventBus();
            Action act = () => bus.Subscribe<PingEvent>(null);
            act.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void Publish_WithNoSubscribers_IsNoOp()
        {
            var bus = new RendererEventBus();
            // Should not throw even with zero subscribers.
            bus.Publish(new PingEvent(42));
        }

        [Fact]
        public void Publish_SingleSubscriber_DeliversPayload()
        {
            var bus = new RendererEventBus();
            int received = 0;
            using var sub = bus.Subscribe<PingEvent>(e => received = e.Value);

            bus.Publish(new PingEvent(7));

            received.Should().Be(7);
        }

        [Fact]
        public void Publish_MultipleSubscribers_FanOutInRegistrationOrder()
        {
            var bus = new RendererEventBus();
            var order = new System.Collections.Generic.List<string>();
            using var a = bus.Subscribe<PingEvent>(_ => order.Add("a"));
            using var b = bus.Subscribe<PingEvent>(_ => order.Add("b"));
            using var c = bus.Subscribe<PingEvent>(_ => order.Add("c"));

            bus.Publish(new PingEvent(1));

            order.Should().Equal("a", "b", "c");
        }

        [Fact]
        public void Publish_DispatchesOnlyToMatchingEventType()
        {
            var bus = new RendererEventBus();
            int pingHits = 0, pongHits = 0;
            using var p = bus.Subscribe<PingEvent>(_ => pingHits++);
            using var q = bus.Subscribe<PongEvent>(_ => pongHits++);

            bus.Publish(new PingEvent(1));
            bus.Publish(new PingEvent(2));
            bus.Publish(new PongEvent("once"));

            pingHits.Should().Be(2);
            pongHits.Should().Be(1);
        }

        [Fact]
        public void Dispose_OnSubscription_UnsubscribesHandler()
        {
            var bus = new RendererEventBus();
            int hits = 0;
            var sub = bus.Subscribe<PingEvent>(_ => hits++);

            bus.Publish(new PingEvent(1));
            sub.Dispose();
            bus.Publish(new PingEvent(2));
            bus.Publish(new PingEvent(3));

            hits.Should().Be(1);
        }

        [Fact]
        public void Dispose_IsIdempotent()
        {
            var bus = new RendererEventBus();
            var sub = bus.Subscribe<PingEvent>(_ => { });
            sub.Dispose();
            sub.Dispose(); // Should not throw.
        }

        [Fact]
        public void HandlerSubscribesDuringDispatch_NewSubscriberDoesNotReceiveCurrentEvent()
        {
            // Snapshot-on-publish: the subscriber list is frozen for the current publish.
            var bus = new RendererEventBus();
            int innerHits = 0;
            IDisposable inner = null;
            using var outer = bus.Subscribe<PingEvent>(_ =>
            {
                inner ??= bus.Subscribe<PingEvent>(__ => innerHits++);
            });

            bus.Publish(new PingEvent(1));
            innerHits.Should().Be(0); // late-bound this dispatch

            bus.Publish(new PingEvent(2));
            innerHits.Should().Be(1);

            inner?.Dispose();
        }

        [Fact]
        public void HandlerUnsubscribesDuringDispatch_StillSeesCurrentEvent()
        {
            // Snapshot-on-publish: the subscriber list is frozen for the current publish,
            // so a handler that disposes itself mid-dispatch is unaffected this round.
            var bus = new RendererEventBus();
            int hits = 0;
            IDisposable self = null;
            self = bus.Subscribe<PingEvent>(_ =>
            {
                hits++;
                self?.Dispose();
            });

            bus.Publish(new PingEvent(1));
            bus.Publish(new PingEvent(2)); // self is now disposed; should not fire.

            hits.Should().Be(1);
        }
    }
}
