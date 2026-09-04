using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Framework.ObjectPooling
{
    // Pure C# throughout: nothing here instantiates a GameObject, so no Object.Destroy is involved
    // and every path can be asserted directly in Edit Mode.
    public class ClassPoolTests
    {
        private int _originalDefaultMaxSize;

        // ══════════════════════════════════════════════
        // Fixture
        // ══════════════════════════════════════════════

        [SetUp]
        public void SetUp()
        {
            _originalDefaultMaxSize = ObjectPoolManager.DefaultMaxSize;
        }

        [TearDown]
        public void TearDown()
        {
            ObjectPoolManager.DefaultMaxSize = _originalDefaultMaxSize;
        }

        // ══════════════════════════════════════════════
        // Construction
        // ══════════════════════════════════════════════

        [Test]
        public void Constructor_NullFactory_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new ClassPool<Payload>(null));
        }

        [Test]
        public void MaxSize_WhenOmitted_FallsBackToTheManagerDefault()
        {
            ObjectPoolManager.DefaultMaxSize = 7;

            Assert.AreEqual(7, new ClassPool<Payload>(NewPayload).MaxSize);
        }

        [Test]
        public void MaxSize_WhenNegative_FallsBackToTheManagerDefault()
        {
            ObjectPoolManager.DefaultMaxSize = 7;

            Assert.AreEqual(7, new ClassPool<Payload>(NewPayload, maxSize: -3).MaxSize);
        }

        [Test]
        public void MaxSize_WhenGiven_Wins()
        {
            ObjectPoolManager.DefaultMaxSize = 7;

            Assert.AreEqual(2, new ClassPool<Payload>(NewPayload, maxSize: 2).MaxSize);
        }

        // ══════════════════════════════════════════════
        // Renting and Returning
        // ══════════════════════════════════════════════

        [Test]
        public void Get_OnAnEmptyPool_UsesTheFactory()
        {
            var pool = new ClassPool<Payload>(NewPayload);

            Assert.IsNotNull(pool.Get());
            Assert.AreEqual(1, pool.TotalRented);
        }

        [Test]
        public void Get_TwoInstances_AreDistinct()
        {
            var pool = new ClassPool<Payload>(NewPayload);

            Assert.AreNotSame(pool.Get(), pool.Get());
        }

        [Test]
        public void Get_AfterReturn_ReusesTheSameInstance()
        {
            var pool = new ClassPool<Payload>(NewPayload);
            Payload first = pool.Get();
            pool.Return(first);

            Assert.AreSame(first, pool.Get());
            Assert.AreEqual(0, pool.Count, "the reserve was not drained by the reuse");
        }

        [Test]
        public void Return_FillsTheReserve()
        {
            var pool = new ClassPool<Payload>(NewPayload);

            pool.Return(pool.Get());

            Assert.AreEqual(1, pool.Count);
            Assert.AreEqual(1, pool.TotalReturned);
        }

        [Test]
        public void Return_Twice_IsIgnored()
        {
            // A double return would queue one instance twice and hand it to two owners.
            var pool = new ClassPool<Payload>(NewPayload);
            Payload instance = pool.Get();

            pool.Return(instance);
            pool.Return(instance);

            Assert.AreEqual(1, pool.Count);
            Assert.AreEqual(1, pool.TotalReturned);
        }

        [Test]
        public void Return_Null_IsIgnored()
        {
            var pool = new ClassPool<Payload>(NewPayload);

            Assert.DoesNotThrow(() => pool.Return(null));
            Assert.AreEqual(0, pool.Count);
        }

        [Test]
        public void Return_TwoInstancesThatCompareEqual_AreBothPooled()
        {
            // Membership must be reference-based. With Equals-based membership the second return
            // would look like a double return and the instance would be silently dropped.
            var pool = new ClassPool<ValueEqualPayload>(() => new ValueEqualPayload());
            ValueEqualPayload first = pool.Get();
            ValueEqualPayload second = pool.Get();

            Assert.AreEqual(first, second, "the fixture needs two instances that compare equal");

            pool.Return(first);
            pool.Return(second);

            Assert.AreEqual(2, pool.Count);
        }

        [Test]
        public void Return_WhenTheReserveIsFull_DropsTheSurplus()
        {
            var pool = new ClassPool<Payload>(NewPayload, maxSize: 1);
            Payload first = pool.Get();
            Payload second = pool.Get();

            pool.Return(first);
            pool.Return(second);

            Assert.AreEqual(1, pool.Count, "the reserve grew past its cap");
            Assert.AreEqual(2, pool.TotalReturned, "the surplus return was not counted");
        }

        [Test]
        public void ActiveCount_TracksRentedMinusReturned()
        {
            var pool = new ClassPool<Payload>(NewPayload);
            Payload first = pool.Get();
            pool.Get();

            Assert.AreEqual(2, pool.ActiveCount);

            pool.Return(first);

            Assert.AreEqual(1, pool.ActiveCount);
        }

        // ══════════════════════════════════════════════
        // Clearing
        // ══════════════════════════════════════════════

        [Test]
        public void Clear_EmptiesTheReserveButKeepsTheCounters()
        {
            var pool = new ClassPool<Payload>(NewPayload);
            pool.Return(pool.Get());

            pool.Clear();

            Assert.AreEqual(0, pool.Count);
            Assert.AreEqual(1, pool.TotalRented, "clearing must not hide a leak that already happened");
            Assert.AreEqual(1, pool.TotalReturned);
        }

        [Test]
        public void Clear_ForgetsWhichInstancesWerePooled()
        {
            var pool = new ClassPool<Payload>(NewPayload);
            Payload instance = pool.Get();
            pool.Return(instance);

            pool.Clear();
            pool.Return(instance);

            Assert.AreEqual(1, pool.Count);
        }

        // ══════════════════════════════════════════════
        // Prewarming
        // ══════════════════════════════════════════════

        [Test]
        public void Prewarm_FillsTheReserveUpFront()
        {
            var pool = new ClassPool<Payload>(NewPayload, initialSize: 3);

            Assert.AreEqual(3, pool.Count);
            Assert.AreEqual(0, pool.TotalRented, "prewarming must not look like renting");
        }

        [Test]
        public void Prewarm_IsClampedToMaxSize()
        {
            var pool = new ClassPool<Payload>(NewPayload, initialSize: 5, maxSize: 2);

            Assert.AreEqual(2, pool.Count);
        }

        [Test]
        public void Prewarm_DoesNotFireOnSpawn()
        {
            // Matches the prefab pool: a prewarmed instance is created, not spawned. Its first Get
            // is what spawns it, so exactly one OnSpawn must have happened by then.
            var pool = new ClassPool<PoolablePayload>(() => new PoolablePayload(), initialSize: 1);

            Assert.AreEqual(1, pool.Get().SpawnCount);
        }

        // ══════════════════════════════════════════════
        // IPoolable
        // ══════════════════════════════════════════════

        [Test]
        public void Get_CallsOnSpawn()
        {
            var pool = new ClassPool<PoolablePayload>(() => new PoolablePayload());

            Assert.AreEqual(1, pool.Get().SpawnCount);
        }

        [Test]
        public void Return_CallsOnReturnToPool()
        {
            var pool = new ClassPool<PoolablePayload>(() => new PoolablePayload());
            PoolablePayload instance = pool.Get();

            pool.Return(instance);

            Assert.AreEqual(1, instance.ReturnCount);
        }

        [Test]
        public void Poolable_IsNotifiedAgainOnEveryReuse()
        {
            // A recycled instance that never gets OnSpawn again keeps the previous life's state.
            var pool = new ClassPool<PoolablePayload>(() => new PoolablePayload());
            PoolablePayload instance = pool.Get();
            pool.Return(instance);
            pool.Get();

            Assert.AreEqual(2, instance.SpawnCount);
            Assert.AreEqual(1, instance.ReturnCount);
        }

        [Test]
        public void Return_Twice_DoesNotNotifyTwice()
        {
            var pool = new ClassPool<PoolablePayload>(() => new PoolablePayload());
            PoolablePayload instance = pool.Get();

            pool.Return(instance);
            pool.Return(instance);

            Assert.AreEqual(1, instance.ReturnCount);
        }

        [Test]
        public void NonPoolableType_CyclesWithoutCallbacks()
        {
            var pool = new ClassPool<Payload>(NewPayload);

            Assert.DoesNotThrow(() => pool.Return(pool.Get()));
        }

        // ══════════════════════════════════════════════
        // Broken Factory
        // ══════════════════════════════════════════════

        [Test]
        public void Get_WhenTheFactoryReturnsNull_ReportsItAndHandsBackNull()
        {
            var pool = new ClassPool<Payload>(() => null);

            LogAssert.Expect(LogType.Error, "[ClassPool] The factory for 'Payload' returned null.");

            Assert.IsNull(pool.Get());
            Assert.AreEqual(0, pool.TotalRented, "a failed Get must not count as rented");
        }

        [Test]
        public void Prewarm_WhenTheFactoryReturnsNull_StopsInsteadOfPoolingNulls()
        {
            LogAssert.Expect(LogType.Error, "[ClassPool] The factory for 'Payload' returned null.");

            var pool = new ClassPool<Payload>(() => null, initialSize: 3);

            Assert.AreEqual(0, pool.Count);
        }

        // ══════════════════════════════════════════════
        // Internal Helpers
        // ══════════════════════════════════════════════

        private static Payload NewPayload() => new Payload();

        // ══════════════════════════════════════════════
        // Test Doubles
        // ══════════════════════════════════════════════

        private sealed class Payload
        {
        }

        private sealed class PoolablePayload : IPoolable
        {
            public int SpawnCount { get; private set; }
            public int ReturnCount { get; private set; }

            public void OnSpawn() => SpawnCount++;

            public void OnReturnToPool() => ReturnCount++;
        }

        // Compares equal to every other instance on purpose, to prove membership is by reference.
        private sealed class ValueEqualPayload
        {
            public override bool Equals(object obj) => obj is ValueEqualPayload;

            public override int GetHashCode() => 1;
        }
    }
}
