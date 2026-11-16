using NUnit.Framework;
using TavlaAI.Board;
using UnityEngine;

namespace TavlaAI.Tests
{
    /// <summary>
    /// Faz 5 Bolum 8, Test 40: 100 karelik simulasyonda <c>Instantiate</c> cagrisi <b>0</b>.
    /// </summary>
    [TestFixture]
    public sealed class CheckerPoolTests
    {
        [Test]
        public void CheckerPool_NoRuntimeInstantiate()
        {
            var go = new GameObject("PoolHost", typeof(RectTransform));
            try
            {
                var pool = new CheckerPool();
                pool.Initialize((RectTransform)go.transform);

                Assert.That(CheckerPool.InstantiateCalls, Is.EqualTo(CheckerPool.Capacity),
                    "havuz kurulumunda tam olarak Capacity kadar Instantiate beklenir");

                CheckerPool.ResetInstantiateCounter();

                for (int frame = 0; frame < 100; frame++)
                {
                    pool.HideAll();
                    for (int i = 0; i < CheckerPool.Capacity; i++)
                    {
                        pool.Place(Color.white, i * 4f, i * 2f, 24f);
                    }
                }

                Assert.That(CheckerPool.InstantiateCalls, Is.EqualTo(0),
                    "kare icinde Instantiate cagrilmamalidir");
                Assert.That(pool.VisibleCount, Is.EqualTo(CheckerPool.Capacity));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void CheckerPool_CapacityIsThirty()
        {
            Assert.That(CheckerPool.Capacity, Is.EqualTo(30));
        }
    }
}
