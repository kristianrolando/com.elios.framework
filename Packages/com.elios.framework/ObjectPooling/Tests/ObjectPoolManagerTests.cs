using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Elios.Framework.ObjectPooling
{
    // Covers the static facade: pool creation on demand, routing a return back to the pool that
    // owns the instance, and dropping pools.
    //
    // ObjectPoolManager.ResetStatics only runs when Play Mode starts, so this fixture clears the
    // pools itself on both sides of every test. Clearing destroys instances, which outside Play
    // Mode does nothing and logs an error, hence the silencing around those calls.
    public class ObjectPoolManagerTests
    {
        private const string PoolRootName = "__ObjectPoolRoot__";

        private GameObject _prefab;
        private GameObject _otherPrefab;
        private int _originalDefaultMaxSize;

        // ══════════════════════════════════════════════
        // Fixture
        // ══════════════════════════════════════════════

        [SetUp]
        public void SetUp()
        {
            ResetPools();

            _originalDefaultMaxSize = ObjectPoolManager.DefaultMaxSize;
            _prefab = new GameObject("ManagedPrefab");
            _otherPrefab = new GameObject("OtherManagedPrefab");
        }

        [TearDown]
        public void TearDown()
        {
            ResetPools();

            ObjectPoolManager.DefaultMaxSize = _originalDefaultMaxSize;
            DestroyIfAlive(_prefab);
            DestroyIfAlive(_otherPrefab);
            DestroyIfAlive(GameObject.Find(PoolRootName));

            LogAssert.ignoreFailingMessages = false;
        }

        // ══════════════════════════════════════════════
        // Pool Creation
        // ══════════════════════════════════════════════

        [Test]
        public void Get_CreatesThePoolOnFirstUse()
        {
            Assert.IsFalse(ObjectPoolManager.HasPool(_prefab));

            ObjectPoolManager.Get(_prefab, Vector3.zero);

            Assert.IsTrue(ObjectPoolManager.HasPool(_prefab), "no setup step should be required before Get");
        }

        [Test]
        public void RegisterPrefab_CreatesThePoolUpFront()
        {
            ObjectPoolManager.RegisterPrefab(_prefab, initialSize: 2);

            Assert.IsTrue(ObjectPoolManager.HasPool(_prefab));
        }

        [Test]
        public void Get_KeepsSeparatePoolsPerPrefab()
        {
            ObjectPoolManager.Get(_prefab, Vector3.zero);

            Assert.IsTrue(ObjectPoolManager.HasPool(_prefab));
            Assert.IsFalse(ObjectPoolManager.HasPool(_otherPrefab), "two prefabs shared one pool");
        }

        [Test]
        public void Get_PlacesTheInstanceAtTheRequestedPosition()
        {
            GameObject instance = ObjectPoolManager.Get(_prefab, new Vector3(0f, 5f, 0f));

            Assert.AreEqual(5f, instance.transform.position.y, 0.0001f);
        }

        // ══════════════════════════════════════════════
        // Guards
        // ══════════════════════════════════════════════

        [Test]
        public void Get_NullPrefab_ReturnsNull()
        {
            Assert.IsNull(ObjectPoolManager.Get(null, Vector3.zero));
        }

        [Test]
        public void HasPool_NullPrefab_IsFalse()
        {
            Assert.IsFalse(ObjectPoolManager.HasPool(null));
        }

        [Test]
        public void RegisterPrefab_NullPrefab_IsIgnored()
        {
            Assert.DoesNotThrow(() => ObjectPoolManager.RegisterPrefab(null));
        }

        [Test]
        public void ClearPoolForPrefab_NullPrefab_IsIgnored()
        {
            Assert.DoesNotThrow(() => ObjectPoolManager.ClearPoolForPrefab(null));
        }

        [Test]
        public void Return_Null_IsIgnored()
        {
            Assert.DoesNotThrow(() => ObjectPoolManager.Return(null));
        }

        // ══════════════════════════════════════════════
        // Routing
        // ══════════════════════════════════════════════

        [Test]
        public void Return_SendsTheInstanceBackToThePoolThatOwnsIt()
        {
            GameObject first = ObjectPoolManager.Get(_prefab, Vector3.zero);
            ObjectPoolManager.Return(first);

            GameObject second = ObjectPoolManager.Get(_prefab, Vector3.zero);

            Assert.AreEqual(first, second, "the returned instance was not reused");
        }

        [Test]
        public void Return_DeactivatesTheInstance()
        {
            GameObject instance = ObjectPoolManager.Get(_prefab, Vector3.zero);

            ObjectPoolManager.Return(instance);

            Assert.IsFalse(instance.activeSelf);
        }

        [Test]
        public void Return_AnObjectThatWasNeverPooled_IsIgnoredSafely()
        {
            LogAssert.ignoreFailingMessages = true;

            var loose = new GameObject("NotPooled");

            Assert.DoesNotThrow(() => ObjectPoolManager.Return(loose));

            DestroyIfAlive(loose);
        }

        // ══════════════════════════════════════════════
        // Clearing
        // ══════════════════════════════════════════════

        [Test]
        public void ClearPoolForPrefab_DropsOnlyThatPool()
        {
            LogAssert.ignoreFailingMessages = true;

            ObjectPoolManager.Get(_prefab, Vector3.zero);
            ObjectPoolManager.Get(_otherPrefab, Vector3.zero);

            ObjectPoolManager.ClearPoolForPrefab(_prefab);

            Assert.IsFalse(ObjectPoolManager.HasPool(_prefab));
            Assert.IsTrue(ObjectPoolManager.HasPool(_otherPrefab));
        }

        [Test]
        public void ClearAll_DropsEveryPool()
        {
            LogAssert.ignoreFailingMessages = true;

            ObjectPoolManager.Get(_prefab, Vector3.zero);
            ObjectPoolManager.Get(_otherPrefab, Vector3.zero);

            ObjectPoolManager.ClearAll();

            Assert.IsFalse(ObjectPoolManager.HasPool(_prefab));
            Assert.IsFalse(ObjectPoolManager.HasPool(_otherPrefab));
        }

        [Test]
        public void Return_AfterItsPoolWasCleared_IsIgnoredSafely()
        {
            // This is what a pooled object still in flight during a scene change runs into.
            LogAssert.ignoreFailingMessages = true;

            GameObject instance = ObjectPoolManager.Get(_prefab, Vector3.zero);
            ObjectPoolManager.ClearAll();

            Assert.DoesNotThrow(() => ObjectPoolManager.Return(instance));

            DestroyIfAlive(instance);
        }

        // ══════════════════════════════════════════════
        // Default Reserve Cap
        // ══════════════════════════════════════════════

        [Test]
        public void DefaultMaxSize_StartsAtTheBuiltInValue()
        {
            Assert.AreEqual(200, ObjectPoolManager.DefaultMaxSize);
        }

        [Test]
        public void DefaultMaxSize_ClampsValuesBelowOne()
        {
            // A pool that can hold nothing is never what a caller meant.
            ObjectPoolManager.DefaultMaxSize = 0;
            Assert.AreEqual(1, ObjectPoolManager.DefaultMaxSize);

            ObjectPoolManager.DefaultMaxSize = -10;
            Assert.AreEqual(1, ObjectPoolManager.DefaultMaxSize);
        }

        [Test]
        public void RegisterPrefab_WithoutAMaxSize_UsesTheDefault()
        {
            // Observed through reuse: with a reserve of one, only the first returned instance can
            // come back out, and the next Get has to build something new.
            LogAssert.ignoreFailingMessages = true;
            ObjectPoolManager.DefaultMaxSize = 1;
            ObjectPoolManager.RegisterPrefab(_prefab);

            GameObject first = ObjectPoolManager.Get(_prefab, Vector3.zero);
            GameObject second = ObjectPoolManager.Get(_prefab, Vector3.zero);
            ObjectPoolManager.Return(first);
            ObjectPoolManager.Return(second);

            Assert.AreEqual(first, ObjectPoolManager.Get(_prefab, Vector3.zero),
                "the one pooled instance was not reused");

            GameObject fresh = ObjectPoolManager.Get(_prefab, Vector3.zero);

            Assert.AreNotEqual(first, fresh);
            Assert.AreNotEqual(second, fresh, "the surplus instance was kept despite the cap");
        }

        [Test]
        public void RegisterPrefab_WithAnExplicitMaxSize_OverridesTheDefault()
        {
            ObjectPoolManager.DefaultMaxSize = 1;
            ObjectPoolManager.RegisterPrefab(_prefab, initialSize: 0, maxSize: 5);

            GameObject first = ObjectPoolManager.Get(_prefab, Vector3.zero);
            GameObject second = ObjectPoolManager.Get(_prefab, Vector3.zero);
            ObjectPoolManager.Return(first);
            ObjectPoolManager.Return(second);

            Assert.AreEqual(first, ObjectPoolManager.Get(_prefab, Vector3.zero));
            Assert.AreEqual(second, ObjectPoolManager.Get(_prefab, Vector3.zero),
                "both instances should fit in a reserve of five");
        }

        // ══════════════════════════════════════════════
        // Internal Helpers
        // ══════════════════════════════════════════════

        private static void ResetPools()
        {
            LogAssert.ignoreFailingMessages = true;
            ObjectPoolManager.ClearAll();
            LogAssert.ignoreFailingMessages = false;
        }

        private static void DestroyIfAlive(GameObject target)
        {
            if (target != null)
                Object.DestroyImmediate(target);
        }
    }
}
