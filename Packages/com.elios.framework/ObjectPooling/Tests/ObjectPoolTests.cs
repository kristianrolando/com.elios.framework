using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Elios.Framework.ObjectPooling
{
    // Exercises ObjectPool directly through its public constructor, so every test owns its own
    // reserve and its own root transform.
    //
    // Three code paths end in Object.Destroy. Outside Play Mode that call does nothing and logs an
    // error, so those tests silence the log and assert the bookkeeping instead: what matters is
    // that the instance never enters the reserve, not that the engine collected it.
    public class ObjectPoolTests
    {
        private const int UnlimitedReserve = 32;

        private GameObject _prefab;
        private GameObject _otherPrefab;
        private Transform _root;

        // ══════════════════════════════════════════════
        // Fixture
        // ══════════════════════════════════════════════

        [SetUp]
        public void SetUp()
        {
            LogAssert.ignoreFailingMessages = false;

            _prefab = BuildPrefab("PoolPrefab");
            _otherPrefab = BuildPrefab("OtherPoolPrefab");
            _root = new GameObject("PoolRoot").transform;
        }

        [TearDown]
        public void TearDown()
        {
            // Every instance lives under the root, so one DestroyImmediate takes the whole reserve.
            DestroyIfAlive(_root != null ? _root.gameObject : null);
            DestroyIfAlive(_prefab);
            DestroyIfAlive(_otherPrefab);

            LogAssert.ignoreFailingMessages = false;
        }

        // ══════════════════════════════════════════════
        // Renting
        // ══════════════════════════════════════════════

        [Test]
        public void Get_ReturnsAnActiveInstance()
        {
            ObjectPool pool = CreatePool();

            GameObject instance = pool.Get(Vector3.zero, Quaternion.identity);

            Assert.IsNotNull(instance);
            Assert.IsTrue(instance.activeSelf);
        }

        [Test]
        public void Get_StampsAPoolableKeyPointingAtThisPool()
        {
            ObjectPool pool = CreatePool();

            GameObject instance = pool.Get(Vector3.zero, Quaternion.identity);

            Assert.IsTrue(instance.TryGetComponent(out PoolableKey key));
            Assert.AreEqual(pool.PoolKey, key.PoolKey);
            Assert.IsFalse(key.IsInPool);
        }

        [Test]
        public void Get_PlacesTheInstanceAtTheRequestedPosition()
        {
            ObjectPool pool = CreatePool();

            Vector3 position = pool.Get(new Vector3(1f, 2f, 3f), Quaternion.identity).transform.position;

            Assert.AreEqual(1f, position.x, 0.0001f);
            Assert.AreEqual(2f, position.y, 0.0001f);
            Assert.AreEqual(3f, position.z, 0.0001f);
        }

        [Test]
        public void Get_WithoutAParent_ParentsUnderThePoolRoot()
        {
            ObjectPool pool = CreatePool();

            GameObject instance = pool.Get(Vector3.zero, Quaternion.identity);

            Assert.AreEqual(_root, instance.transform.parent);
        }

        [Test]
        public void Get_WithAParent_UsesThatParentInstead()
        {
            ObjectPool pool = CreatePool();
            var customParent = new GameObject("CustomParent").transform;
            customParent.SetParent(_root, false);

            GameObject instance = pool.Get(Vector3.zero, Quaternion.identity, customParent);

            Assert.AreEqual(customParent, instance.transform.parent);
        }

        [Test]
        public void Get_OnAnEmptyPool_CreatesAFreshInstance()
        {
            ObjectPool pool = CreatePool();

            pool.Get(Vector3.zero, Quaternion.identity);

            Assert.AreEqual(0, pool.Count, "an instance that was never returned must not sit in the reserve");
            Assert.AreEqual(1, pool.TotalRented);
        }

        [Test]
        public void Get_TwoInstances_AreDistinct()
        {
            ObjectPool pool = CreatePool();

            GameObject first = pool.Get(Vector3.zero, Quaternion.identity);
            GameObject second = pool.Get(Vector3.zero, Quaternion.identity);

            Assert.AreNotEqual(first, second);
        }

        // ══════════════════════════════════════════════
        // Returning
        // ══════════════════════════════════════════════

        [Test]
        public void Return_DeactivatesTheInstanceAndFillsTheReserve()
        {
            ObjectPool pool = CreatePool();
            GameObject instance = pool.Get(Vector3.zero, Quaternion.identity);

            pool.Return(instance);

            Assert.IsFalse(instance.activeSelf);
            Assert.AreEqual(1, pool.Count);
        }

        [Test]
        public void Return_MarksTheInstanceAsPooled()
        {
            ObjectPool pool = CreatePool();
            GameObject instance = pool.Get(Vector3.zero, Quaternion.identity);

            pool.Return(instance);

            Assert.IsTrue(instance.GetComponent<PoolableKey>().IsInPool);
        }

        [Test]
        public void Get_AfterReturn_ReusesTheSameInstance()
        {
            ObjectPool pool = CreatePool();
            GameObject first = pool.Get(Vector3.zero, Quaternion.identity);
            pool.Return(first);

            GameObject second = pool.Get(Vector3.zero, Quaternion.identity);

            Assert.AreEqual(first, second);
            Assert.AreEqual(0, pool.Count, "the reserve was not drained by the reuse");
        }

        [Test]
        public void Return_Twice_IsIgnored()
        {
            // A double return would enqueue the same instance twice and hand it out to two owners.
            ObjectPool pool = CreatePool();
            GameObject instance = pool.Get(Vector3.zero, Quaternion.identity);

            pool.Return(instance);
            pool.Return(instance);

            Assert.AreEqual(1, pool.Count);
            Assert.AreEqual(1, pool.TotalReturned);
        }

        [Test]
        public void Return_Null_IsIgnored()
        {
            ObjectPool pool = CreatePool();

            Assert.DoesNotThrow(() => pool.Return(null));
            Assert.AreEqual(0, pool.Count);
        }

        [Test]
        public void ActiveCount_TracksRentedMinusReturned()
        {
            ObjectPool pool = CreatePool();
            GameObject first = pool.Get(Vector3.zero, Quaternion.identity);
            pool.Get(Vector3.zero, Quaternion.identity);

            Assert.AreEqual(2, pool.ActiveCount);

            pool.Return(first);

            Assert.AreEqual(1, pool.ActiveCount, "a climbing ActiveCount is how a pool leak shows up");
        }

        [Test]
        public void Get_SkipsInstancesDestroyedWhileSittingInTheReserve()
        {
            ObjectPool pool = CreatePool();
            GameObject pooled = pool.Get(Vector3.zero, Quaternion.identity);
            pool.Return(pooled);

            Object.DestroyImmediate(pooled);

            GameObject fresh = pool.Get(Vector3.zero, Quaternion.identity);

            Assert.IsNotNull(fresh, "the pool handed out a destroyed instance");
            Assert.IsTrue(fresh.activeSelf);
        }

        // ══════════════════════════════════════════════
        // Reserve Size
        // ══════════════════════════════════════════════

        [Test]
        public void Prewarm_FillsTheReserveUpFront()
        {
            var pool = new ObjectPool(_prefab, initialSize: 3, maxSize: UnlimitedReserve, rootParent: _root);

            Assert.AreEqual(3, pool.Count);
            Assert.AreEqual(0, pool.TotalRented, "prewarming must not look like renting");
        }

        [Test]
        public void Prewarm_IsClampedToMaxSize()
        {
            // Creating instances only to destroy them again would be pure waste.
            var pool = new ObjectPool(_prefab, initialSize: 5, maxSize: 2, rootParent: _root);

            Assert.AreEqual(2, pool.Count);
        }

        [Test]
        public void MaxSize_IsNeverBelowOne()
        {
            var pool = new ObjectPool(_prefab, initialSize: 0, maxSize: 0, rootParent: _root);

            pool.Return(pool.Get(Vector3.zero, Quaternion.identity));

            Assert.AreEqual(1, pool.Count);
        }

        [Test]
        public void Return_WhenTheReserveIsFull_DoesNotGrowBeyondMaxSize()
        {
            LogAssert.ignoreFailingMessages = true;

            var pool = new ObjectPool(_prefab, initialSize: 0, maxSize: 1, rootParent: _root);
            GameObject first = pool.Get(Vector3.zero, Quaternion.identity);
            GameObject second = pool.Get(Vector3.zero, Quaternion.identity);

            pool.Return(first);
            pool.Return(second);

            Assert.AreEqual(1, pool.Count, "the reserve grew past its cap");
            Assert.AreEqual(2, pool.TotalReturned, "the surplus return was not counted");
        }

        // ══════════════════════════════════════════════
        // Rejected Returns
        // ══════════════════════════════════════════════

        [Test]
        public void Return_AnInstanceBelongingToAnotherPool_IsRejected()
        {
            LogAssert.ignoreFailingMessages = true;

            ObjectPool owner = CreatePool();
            var stranger = new ObjectPool(_otherPrefab, 0, UnlimitedReserve, _root);
            GameObject instance = owner.Get(Vector3.zero, Quaternion.identity);

            stranger.Return(instance);

            Assert.AreEqual(0, stranger.Count, "an instance from another pool entered the reserve");
            Assert.AreEqual(0, stranger.TotalReturned);
        }

        [Test]
        public void Return_AnObjectThatWasNeverPooled_IsRejected()
        {
            LogAssert.ignoreFailingMessages = true;

            ObjectPool pool = CreatePool();
            var loose = new GameObject("NotPooled");
            loose.transform.SetParent(_root, false);

            pool.Return(loose);

            Assert.AreEqual(0, pool.Count);
            Assert.AreEqual(0, pool.TotalReturned);
        }

        // ══════════════════════════════════════════════
        // Poolable Callbacks
        // ══════════════════════════════════════════════

        [Test]
        public void Get_NotifiesPoolablesOnTheRootAndOnChildren()
        {
            ObjectPool pool = CreatePool();

            GameObject instance = pool.Get(Vector3.zero, Quaternion.identity);

            foreach (PoolableProbe probe in instance.GetComponentsInChildren<PoolableProbe>(true))
                Assert.AreEqual(1, probe.SpawnCount, $"'{probe.name}' was not notified on spawn");
        }

        [Test]
        public void Return_NotifiesPoolablesOnTheRootAndOnChildren()
        {
            ObjectPool pool = CreatePool();
            GameObject instance = pool.Get(Vector3.zero, Quaternion.identity);

            pool.Return(instance);

            foreach (PoolableProbe probe in instance.GetComponentsInChildren<PoolableProbe>(true))
                Assert.AreEqual(1, probe.ReturnCount, $"'{probe.name}' was not notified on return");
        }

        [Test]
        public void Poolables_AreNotifiedAgainOnEveryReuse()
        {
            // A recycled instance that never gets OnSpawn again keeps the previous life's state.
            ObjectPool pool = CreatePool();
            GameObject instance = pool.Get(Vector3.zero, Quaternion.identity);
            pool.Return(instance);
            pool.Get(Vector3.zero, Quaternion.identity);

            Assert.AreEqual(2, instance.GetComponent<PoolableProbe>().SpawnCount);
        }

        // ══════════════════════════════════════════════
        // Internal Helpers
        // ══════════════════════════════════════════════

        private ObjectPool CreatePool()
        {
            return new ObjectPool(_prefab, 0, UnlimitedReserve, _root);
        }

        private static GameObject BuildPrefab(string name)
        {
            var root = new GameObject(name);
            root.AddComponent<PoolableProbe>();

            var child = new GameObject(name + "_Child");
            child.transform.SetParent(root.transform, false);
            child.AddComponent<PoolableProbe>();

            return root;
        }

        private static void DestroyIfAlive(GameObject target)
        {
            if (target != null)
                Object.DestroyImmediate(target);
        }
    }

    // Top-level on purpose: a nested MonoBehaviour is a special case for Unity, and this one only
    // needs to count the two pool callbacks.
    internal class PoolableProbe : MonoBehaviour, IPoolable
    {
        public int SpawnCount { get; private set; }
        public int ReturnCount { get; private set; }

        public void OnSpawn() => SpawnCount++;

        public void OnReturnToPool() => ReturnCount++;
    }
}
