using System.Collections;
using Backgammon.Core;
using Backgammon.UI;
using NUnit.Framework;
using TavlaAI.Bootstrap;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace TavlaAI.Tests
{
    /// <summary>
    /// Faz 5 Bolum 6.12: <b>tam oyun</b> testi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="SmokeTests"/> yalnizca 60 kare calisir; bu test bir oyunun
    /// <see cref="GamePhase.GameOver"/> fazina <b>gercekten</b> ulastigini dogrular.
    /// </para>
    /// <para>
    /// Insan tarafi da yapay zeka ile oynatilir; boylece bar girisinden toplamaya kadar
    /// tum fazlar ve tum hamle uretimi zinciri calistirilmis olur.
    /// </para>
    /// </remarks>
    [TestFixture]
    public sealed class FullGameTests
    {
        private const int MaxSteps = 20000;

        [UnityTest]
        public IEnumerator FullGame_ReachesGameOver()
        {
            SceneManager.LoadScene("Main");
            yield return null;
            yield return null;

            GameBootstrap bootstrap = GameBootstrap.Instance;
            Assert.That(bootstrap, Is.Not.Null, "GameBootstrap kurulmadi");

            GameOrchestrator orchestrator = bootstrap.Orchestrator;
            Assert.That(orchestrator, Is.Not.Null, "GameOrchestrator kurulmadi");

            // Test hizli kalsin: kok arama ve analiz derinligini sifirla.
            orchestrator.Options.SearchSettings.DepthPly = 0;
            orchestrator.Options.MaxPlies = 0;
            orchestrator.NewMatch(0, false);

            int steps = 0;
            while (!orchestrator.IsGameOver && steps < MaxSteps)
            {
                BackgammonState state = orchestrator.State;

                if (state.phase == GamePhase.DetermineFirstPlayer)
                {
                    orchestrator.RollDice();
                }
                else if (state.phase == GamePhase.PreRoll || state.phase == GamePhase.WaitForRoll)
                {
                    orchestrator.RollDice();
                }
                else if (state.phase == GamePhase.CubeOffered)
                {
                    orchestrator.Take();
                }
                else if (state.phase == GamePhase.Moving)
                {
                    SearchOutcome outcome = orchestrator.Think();
                    if (outcome.IsPass || outcome.BestSequence.IsEmpty)
                    {
                        orchestrator.EndTurn();
                    }
                    else
                    {
                        orchestrator.ApplyMove(outcome.BestSequence);
                    }
                }
                else
                {
                    break;
                }

                steps++;
                if (steps % 100 == 0)
                {
                    yield return null;
                }
            }

            Assert.That(orchestrator.IsGameOver, Is.True,
                "oyun " + steps + " adimda GameOver fazina ulasmadi");
            Assert.That(orchestrator.Result.IsValid, Is.True,
                "oyun sonucu gecersiz: " + orchestrator.Result);
            Assert.That(orchestrator.Result.Points, Is.GreaterThanOrEqualTo(1),
                "mars carpani en az 1 olmali");
        }
    }
}
