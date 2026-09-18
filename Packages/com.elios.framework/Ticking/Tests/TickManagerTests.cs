using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Elios.Framework.Ticking
{
    // Drives the loop through TickManager's internal Run* entry points, which is what TickDriver
    // calls every frame. Nothing here depends on the editor actually rendering a frame.
    public class TickManagerTests
    {
        private const string DriverName = "__TickDriver__";
        private const float LongInterval = 100f;

        private readonly List<GameObject> _spawned = new List<GameObject>();

        // ══════════════════════════════════════════════
        // Fixture
        // ══════════════════════════════════════════════

        [SetUp]
        public void SetUp()
        {
            TickManager.Clear();
            TickManager.IsPaused = false;
        }

        [TearDown]
        public void TearDown()
        {
            TickManager.Clear();
            TickManager.IsPaused = false;

            for (int i = 0; i < _spawned.Count; i++)
            {
                if (_spawned[i] != null)
                    UnityEngine.Object.DestroyImmediate(_spawned[i]);
            }
            _spawned.Clear();

            // Registering creates the driver on demand; it must not leak into the open scene.
            GameObject driver = GameObject.Find(DriverName);
            if (driver != null)
                UnityEngine.Object.DestroyImmediate(driver);
        }

        // ══════════════════════════════════════════════
        // Registration
        // ══════════════════════════════════════════════

        [Test]
        public void RegisterTick_TargetIsTickedOncePerUpdate()
        {
            var target = new CountingTickable();

            TickManager.RegisterTick(target);
            TickManager.RunUpdate();

            Assert.AreEqual(1, target.Ticks);
        }

        [Test]
        public void RegisterTick_Twice_StillTicksOnlyOnce()
        {
            var target = new CountingTickable();

            TickManager.RegisterTick(target);
            TickManager.RegisterTick(target);
            TickManager.RunUpdate();

            Assert.AreEqual(1, target.Ticks, "a duplicate registration double-ticked the target");
        }

        [Test]
        public void UnregisterTick_StopsTheTarget()
        {
            var target = new CountingTickable();

            TickManager.RegisterTick(target);
            TickManager.UnregisterTick(target);
            TickManager.RunUpdate();

            Assert.AreEqual(0, target.Ticks);
        }

        [Test]
        public void UnregisterTick_UnknownTarget_IsIgnored()
        {
            Assert.DoesNotThrow(() => TickManager.UnregisterTick(new CountingTickable()));
        }

        [Test]
        public void IsRegistered_TracksRegistrationState()
        {
            var target = new CountingTickable();

            Assert.IsFalse(TickManager.IsRegistered(target));

            TickManager.RegisterTick(target);
            Assert.IsTrue(TickManager.IsRegistered(target));

            TickManager.UnregisterTick(target);
            Assert.IsFalse(TickManager.IsRegistered(target));
        }

        [Test]
        public void RegisterTick_Null_LogsAnErrorAndRegistersNothing()
        {
            LogAssert.Expect(LogType.Error, "[TickManager] Tried to register a null ITickable.");

            Assert.DoesNotThrow(() => TickManager.RegisterTick(null));
            Assert.DoesNotThrow(TickManager.RunUpdate);
        }

        [Test]
        public void Clear_EmptiesEveryChannel()
        {
            var scaled = new CountingTickable();
            var unscaled = new CountingUnscaledTickable();
            var fixedTick = new CountingFixedTickable();
            var late = new CountingLateTickable();

            TickManager.RegisterTick(scaled);
            TickManager.RegisterUnscaledTick(unscaled);
            TickManager.RegisterFixedTick(fixedTick);
            TickManager.RegisterLateTick(late);

            TickManager.Clear();

            TickManager.RunUpdate();
            TickManager.RunFixedUpdate();
            TickManager.RunLateUpdate();

            Assert.AreEqual(0, scaled.Ticks);
            Assert.AreEqual(0, unscaled.Ticks);
            Assert.AreEqual(0, fixedTick.Ticks);
            Assert.AreEqual(0, late.Ticks);
        }

        // ══════════════════════════════════════════════
        // Channels
        // ══════════════════════════════════════════════

        [Test]
        public void RunUpdate_DrivesBothTheScaledAndTheUnscaledChannel()
        {
            var scaled = new CountingTickable();
            var unscaled = new CountingUnscaledTickable();

            TickManager.RegisterTick(scaled);
            TickManager.RegisterUnscaledTick(unscaled);
            TickManager.RunUpdate();

            Assert.AreEqual(1, scaled.Ticks);
            Assert.AreEqual(1, unscaled.Ticks);
        }

        [Test]
        public void RunFixedUpdate_DrivesOnlyTheFixedChannel()
        {
            var scaled = new CountingTickable();
            var fixedTick = new CountingFixedTickable();

            TickManager.RegisterTick(scaled);
            TickManager.RegisterFixedTick(fixedTick);
            TickManager.RunFixedUpdate();

            Assert.AreEqual(0, scaled.Ticks);
            Assert.AreEqual(1, fixedTick.Ticks);
        }

        [Test]
        public void RunLateUpdate_DrivesOnlyTheLateChannel()
        {
            var scaled = new CountingTickable();
            var late = new CountingLateTickable();

            TickManager.RegisterTick(scaled);
            TickManager.RegisterLateTick(late);
            TickManager.RunLateUpdate();

            Assert.AreEqual(0, scaled.Ticks);
            Assert.AreEqual(1, late.Ticks);
        }

        // ══════════════════════════════════════════════
        // Pause
        // ══════════════════════════════════════════════

        [Test]
        public void IsPaused_FreezesTheScaledChannelButNotTheUnscaledOne()
        {
            // The unscaled channel is what keeps menus and timers alive during a pause.
            var scaled = new CountingTickable();
            var unscaled = new CountingUnscaledTickable();

            TickManager.RegisterTick(scaled);
            TickManager.RegisterUnscaledTick(unscaled);
            TickManager.IsPaused = true;
            TickManager.RunUpdate();

            Assert.AreEqual(0, scaled.Ticks);
            Assert.AreEqual(1, unscaled.Ticks);
        }

        [Test]
        public void IsPaused_FreezesTheFixedChannel()
        {
            var fixedTick = new CountingFixedTickable();

            TickManager.RegisterFixedTick(fixedTick);
            TickManager.IsPaused = true;
            TickManager.RunFixedUpdate();

            Assert.AreEqual(0, fixedTick.Ticks);
        }

        [Test]
        public void IsPaused_FreezesTheLateChannel()
        {
            var late = new CountingLateTickable();

            TickManager.RegisterLateTick(late);
            TickManager.IsPaused = true;
            TickManager.RunLateUpdate();

            Assert.AreEqual(0, late.Ticks);
        }

        [Test]
        public void IsPaused_Cleared_ResumesTheScaledChannel()
        {
            var scaled = new CountingTickable();

            TickManager.RegisterTick(scaled);
            TickManager.IsPaused = true;
            TickManager.RunUpdate();
            TickManager.IsPaused = false;
            TickManager.RunUpdate();

            Assert.AreEqual(1, scaled.Ticks);
        }

        // ══════════════════════════════════════════════
        // Mutating from Inside a Tick
        // ══════════════════════════════════════════════

        [Test]
        public void RegisterFromInsideATick_DefersTheNewTargetToTheNextFrame()
        {
            // Appending straight into the array being iterated would tick the newcomer in the same
            // frame and, on a resize, invalidate the loop.
            var newcomer = new CountingTickable();
            var registrar = new RegisteringTickable(newcomer);

            TickManager.RegisterTick(registrar);
            TickManager.RunUpdate();

            Assert.AreEqual(0, newcomer.Ticks, "the newcomer was ticked in the frame it was added");

            TickManager.RunUpdate();

            Assert.AreEqual(1, newcomer.Ticks, "the newcomer never joined the channel");
        }

        [Test]
        public void UnregisterFromInsideATick_SkipsATargetThatHasNotRunYet()
        {
            var victim = new CountingTickable();
            var remover = new RemovingTickable(victim);

            TickManager.RegisterTick(remover);
            TickManager.RegisterTick(victim);
            TickManager.RunUpdate();

            Assert.AreEqual(0, victim.Ticks, "a target removed mid-tick still ran this frame");
        }

        [Test]
        public void UnregisterFromInsideATick_KeepsTheRestOfTheChannelRunning()
        {
            var victim = new CountingTickable();
            var survivor = new CountingTickable();
            var remover = new RemovingTickable(victim);

            TickManager.RegisterTick(remover);
            TickManager.RegisterTick(victim);
            TickManager.RegisterTick(survivor);
            TickManager.RunUpdate();

            Assert.AreEqual(1, survivor.Ticks);
        }

        [Test]
        public void SelfUnregisterFromInsideATick_StopsAfterTheCurrentFrame()
        {
            var target = new SelfRemovingTickable();

            TickManager.RegisterTick(target);
            TickManager.RunUpdate();
            TickManager.RunUpdate();

            Assert.AreEqual(1, target.Ticks);
            Assert.IsFalse(TickManager.IsRegistered(target));
        }

        [Test]
        public void ClearFromInsideATick_DoesNotThrow()
        {
            var target = new ClearingTickable();
            var other = new CountingTickable();

            TickManager.RegisterTick(target);
            TickManager.RegisterTick(other);

            Assert.DoesNotThrow(TickManager.RunUpdate);
        }

        // ══════════════════════════════════════════════
        // Dead and Broken Targets
        // ══════════════════════════════════════════════

        [Test]
        public void DestroyedMonoBehaviour_IsDroppedOnTheNextTick()
        {
            // A destroyed component is only fake-null, so plain reference equality would happily
            // keep ticking it forever.
            MonoTickable target = SpawnMonoTickable();
            TickManager.RegisterTick(target);

            UnityEngine.Object.DestroyImmediate(target.gameObject);

            Assert.DoesNotThrow(TickManager.RunUpdate);
            Assert.IsFalse(TickManager.IsRegistered(target), "a destroyed target stayed registered");
        }

        [Test]
        public void DestroyedMonoBehaviour_DoesNotStopTheRestOfTheChannel()
        {
            MonoTickable dead = SpawnMonoTickable();
            var survivor = new CountingTickable();

            TickManager.RegisterTick(dead);
            TickManager.RegisterTick(survivor);
            UnityEngine.Object.DestroyImmediate(dead.gameObject);

            TickManager.RunUpdate();

            Assert.AreEqual(1, survivor.Ticks);
        }

        [Test]
        public void ThrowingTarget_IsUnregisteredAndDoesNotStopTheRest()
        {
            var thrower = new ThrowingTickable();
            var survivor = new CountingTickable();

            TickManager.RegisterTick(thrower);
            TickManager.RegisterTick(survivor);

            LogAssert.Expect(LogType.Error, new Regex("threw during ITickable and was unregistered"));
            TickManager.RunUpdate();

            Assert.AreEqual(1, survivor.Ticks, "one broken target stopped the whole channel");

            // Second frame: the offender is gone, so it must not report the same error again.
            TickManager.RunUpdate();

            Assert.AreEqual(2, survivor.Ticks);
            Assert.IsFalse(TickManager.IsRegistered(thrower));
        }

        // ══════════════════════════════════════════════
        // Interval Throttling
        // ══════════════════════════════════════════════

        [Test]
        public void PositiveInterval_HoldsTheFirstTickBackUntilTheIntervalElapses()
        {
            var target = new CountingTickable();

            TickManager.RegisterTick(target, LongInterval);
            TickManager.RunUpdate();

            Assert.AreEqual(0, target.Ticks, "a throttled target ticked before its interval elapsed");
        }

        [Test]
        public void IntervalBelowTheMinimum_IsTreatedAsEveryFrame()
        {
            // Guards the MinInterval clamp: a mistyped tiny interval must not turn into a
            // division-sized accumulation error.
            var target = new CountingTickable();

            TickManager.RegisterTick(target, 0.00001f);
            TickManager.RunUpdate();

            Assert.AreEqual(1, target.Ticks);
        }

        [Test]
        public void ZeroInterval_TicksEveryFrame()
        {
            var target = new CountingTickable();

            TickManager.RegisterTick(target, 0f);
            TickManager.RunUpdate();
            TickManager.RunUpdate();

            Assert.AreEqual(2, target.Ticks);
        }

        // ══════════════════════════════════════════════
        // Internal Helpers
        // ══════════════════════════════════════════════

        private MonoTickable SpawnMonoTickable()
        {
            var go = new GameObject("MonoTickable");
            _spawned.Add(go);
            return go.AddComponent<MonoTickable>();
        }

        // ══════════════════════════════════════════════
        // Test Doubles
        // ══════════════════════════════════════════════

        private class CountingTickable : ITickable
        {
            public int Ticks { get; private set; }
            public void Tick(float deltaTime) => Ticks++;
        }

        private class CountingUnscaledTickable : IUnscaledTickable
        {
            public int Ticks { get; private set; }
            public void UnscaledTick(float unscaledDeltaTime) => Ticks++;
        }

        private class CountingFixedTickable : IFixedTickable
        {
            public int Ticks { get; private set; }
            public void FixedTick(float fixedDeltaTime) => Ticks++;
        }

        private class CountingLateTickable : ILateTickable
        {
            public int Ticks { get; private set; }
            public void LateTick(float deltaTime) => Ticks++;
        }

        private class RegisteringTickable : ITickable
        {
            private readonly ITickable _newcomer;
            private bool _done;

            public RegisteringTickable(ITickable newcomer) => _newcomer = newcomer;

            public void Tick(float deltaTime)
            {
                if (_done) return;

                _done = true;
                TickManager.RegisterTick(_newcomer);
            }
        }

        private class RemovingTickable : ITickable
        {
            private readonly ITickable _victim;

            public RemovingTickable(ITickable victim) => _victim = victim;

            public void Tick(float deltaTime) => TickManager.UnregisterTick(_victim);
        }

        private class SelfRemovingTickable : ITickable
        {
            public int Ticks { get; private set; }

            public void Tick(float deltaTime)
            {
                Ticks++;
                TickManager.UnregisterTick(this);
            }
        }

        private class ClearingTickable : ITickable
        {
            public void Tick(float deltaTime) => TickManager.Clear();
        }

        private class ThrowingTickable : ITickable
        {
            public void Tick(float deltaTime) => throw new InvalidOperationException("deliberate test failure");
        }
    }

    // Top-level on purpose: Unity treats a nested MonoBehaviour as a special case, and this one
    // only exists to be destroyed while still registered.
    internal class MonoTickable : MonoBehaviour, ITickable
    {
        public void Tick(float deltaTime) { }
    }
}
