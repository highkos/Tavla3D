using System.Collections;
using Backgammon.Core;
using NUnit.Framework;
using TavlaAI.Bootstrap;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace TavlaAI.Tests
{
    /// <summary>
    /// Faz 5 Bolum 6.12: <c>Main.unity</c> sahnesinin <b>duman testi</b>.
    /// </summary>
    /// <remarks>
    /// Sahne yuklenir, <see cref="GameBootstrap"/> kurulur, 60 kare boyunca hata/istisna
    /// olmadan calisir ve orkestrator gecerli bir durumda kalir.
    /// </remarks>
    [TestFixture]
    public sealed class SmokeTests
    {
        [UnityTest]
        public IEnumerator MainScene_SmokeTest()
        {
            SceneManager.LoadScene("Main");
            yield return null;
            yield return null;

            GameBootstrap bootstrap = GameBootstrap.Instance;
            Assert.That(bootstrap, Is.Not.Null, "GameBootstrap kurulmadi");
            Assert.That(bootstrap.Orchestrator, Is.Not.Null, "GameOrchestrator kurulmadi");
            Assert.That(bootstrap.BoardView, Is.Not.Null, "BoardView kurulmadi");
            Assert.That(bootstrap.BoardView.Points.Length, Is.EqualTo(24), "24 hane beklenir");
            Assert.That(bootstrap.DiceView, Is.Not.Null, "DiceView kurulmadi");
            Assert.That(bootstrap.CubeView, Is.Not.Null, "CubeView kurulmadi");
            Assert.That(bootstrap.MatchScoreView, Is.Not.Null, "MatchScoreView kurulmadi");
            Assert.That(bootstrap.CrawfordBadgeView, Is.Not.Null, "CrawfordBadgeView kurulmadi");
            Assert.That(bootstrap.AnalysisPanelView, Is.Not.Null, "AnalysisPanelView kurulmadi");

            for (int frame = 0; frame < 60; frame++)
            {
                yield return null;
            }

            BackgammonState state = bootstrap.Orchestrator.State;
            Assert.That(state, Is.Not.Null);
            Assert.That(state.phase, Is.Not.EqualTo(GamePhase.DetermineFirstPlayer),
                "acilis fazinda takildi");
            Assert.That(bootstrap.BoardView.Pool.VisibleCount, Is.GreaterThan(0),
                "tahtada hic tas cizilmedi");
        }
    }
}
