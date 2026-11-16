using NUnit.Framework;
using TavlaAI.Analysis;
using TavlaAI.Board;
using TavlaAI.Bootstrap;
using TavlaAI.Cube;
using TavlaAI.Dice;
using TavlaAI.Hud;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TavlaAI.Tests
{
    /// <summary>
    /// Faz 5 Bolum 8, Test 39: <c>Main.unity</c> yuklenir ve zorunlu bilesenler sahnede vardir.
    /// </summary>
    [TestFixture]
    public sealed class SceneLoadsTests
    {
        private const string ScenePath = "Assets/Scenes/Main.unity";

        [Test]
        public void SceneLoads()
        {
            Assert.That(System.IO.File.Exists(ScenePath), Is.True,
                "sahne dosyasi yok; once TavlaAI.Editor.SceneBuilder.Build calistirilmalidir");

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Assert.That(scene.IsValid(), Is.True, "sahne gecersiz");

            AssertPresent<GameBootstrap>(scene, "GameBootstrap");
            AssertPresent<BoardView>(scene, "BoardView");
            AssertPresent<DiceView>(scene, "DiceView");
            AssertPresent<CubeView>(scene, "CubeView");
            AssertPresent<MatchScoreView>(scene, "MatchScoreView");
            AssertPresent<CrawfordBadgeView>(scene, "CrawfordBadgeView");
            AssertPresent<AnalysisPanelView>(scene, "AnalysisPanelView");
        }

        [Test]
        public void SceneHasSingleGameBootstrap()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            GameBootstrap[] found = Object.FindObjectsByType<GameBootstrap>(FindObjectsSortMode.None);
            Assert.That(found.Length, Is.EqualTo(1),
                "sahnede tam olarak bir GameBootstrap olmalidir (Bolum 6.3)");
        }

        private static void AssertPresent<T>(Scene scene, string label) where T : Component
        {
            T[] found = Object.FindObjectsByType<T>(FindObjectsSortMode.None);
            Assert.That(found.Length, Is.GreaterThanOrEqualTo(1), label + " sahnede yok");
        }
    }
}
