using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Backgammon.Core;
using Backgammon.UI;
using NUnit.Framework;
using TavlaAI.Bootstrap;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace TavlaAI.Tests
{
    /// <summary>
    /// Zar rastgeleligi regresyon testleri (Unity tarafi).
    /// </summary>
    /// <remarks>
    /// <b>Hata:</b> <see cref="GameBootstrap"/> orkestratoru <b>sabit</b> bir tohumla
    /// (<c>0x7A71A_0005</c>) kuruyordu. Bu yuzden her uygulama acilisinda ayni zar dizisi
    /// geliyordu (ornegin once <c>5-2</c>, sonra <c>6-6</c>). Motor deterministiktir ve tohumu
    /// disaridan alir; dogru davranis, canli oyunda tohumun isletim sisteminden uretilmesidir.
    /// </remarks>
    [TestFixture]
    public sealed class GameplayFix8PlayModeTests
    {
        private const ulong OldFixedSeed = 0x7A71A_0005UL;

        [UnityTest]
        public IEnumerator Dice_AreNotFromTheOldFixedSeed()
        {
            yield return LoadMainScene();

            GameBootstrap bootstrap = GameBootstrap.Instance;
            Assert.That(bootstrap, Is.Not.Null, "GameBootstrap kurulmadi");

            GameOrchestrator orchestrator = GetPrivateField<GameOrchestrator>(bootstrap, "_orchestrator");
            Assert.That(orchestrator, Is.Not.Null);

            IRandom live = GetRandom(orchestrator);
            Assert.That(live, Is.Not.Null, "orkestratorun rastgelelik kaynagi bulunamadi");

            string liveSequence = Sequence(live);
            string oldSequence = Sequence(new Xoshiro256StarStar(OldFixedSeed));

            Assert.That(liveSequence, Is.Not.EqualTo(oldSequence),
                        "canli oyun eski sabit tohumun dizisini uretmemeli");
        }

        [UnityTest]
        public IEnumerator Dice_OpeningRollsVaryAcrossNewGames()
        {
            yield return LoadMainScene();

            GameBootstrap bootstrap = GameBootstrap.Instance;
            Assert.That(bootstrap, Is.Not.Null, "GameBootstrap kurulmadi");

            GameOrchestrator orchestrator = GetPrivateField<GameOrchestrator>(bootstrap, "_orchestrator");
            Assert.That(orchestrator, Is.Not.Null);

            var openings = new HashSet<string>();

            for (int i = 0; i < 8; i++)
            {
                orchestrator.NewGame();
                Assert.That(orchestrator.State.phase, Is.EqualTo(GamePhase.DetermineFirstPlayer),
                            "yeni oyun acilis zarindan baslamali");

                orchestrator.RollDice();
                BackgammonState state = orchestrator.State;
                openings.Add(state.rolledDice[0] + "-" + state.rolledDice[1]);
            }

            Assert.That(openings.Count, Is.GreaterThan(1),
                        "8 acilis zari ayni cikti: rastgelelik calismiyor");
        }

        [UnityTest]
        public IEnumerator Dice_SuccessiveRollsVary()
        {
            yield return LoadMainScene();

            GameBootstrap bootstrap = GameBootstrap.Instance;
            Assert.That(bootstrap, Is.Not.Null, "GameBootstrap kurulmadi");

            GameOrchestrator orchestrator = GetPrivateField<GameOrchestrator>(bootstrap, "_orchestrator");
            Assert.That(orchestrator, Is.Not.Null);

            IRandom live = GetRandom(orchestrator);
            var rolls = new HashSet<string>();

            for (int i = 0; i < 40; i++)
            {
                rolls.Add(live.RollDie() + "-" + live.RollDie());
            }

            Assert.That(rolls.Count, Is.GreaterThan(10),
                        "40 zarda en az 11 farkli cift beklenir; sabit dizi tek eleman verir");
        }

        // ------------------------------------------------------------------
        // Yardimcilar
        // ------------------------------------------------------------------

        private static IRandom GetRandom(GameOrchestrator orchestrator)
        {
            FieldInfo optionsField = typeof(GameOrchestrator)
                .GetField("_options", BindingFlags.NonPublic | BindingFlags.Instance);
            if (optionsField == null)
            {
                return null;
            }

            var options = optionsField.GetValue(orchestrator) as GameOrchestratorOptions;
            return options == null ? null : options.Random;
        }

        private static string Sequence(IRandom random)
        {
            var parts = new string[24];
            for (int i = 0; i < parts.Length; i++)
            {
                parts[i] = random.RollDie().ToString();
            }

            return string.Join("-", parts);
        }

        private static T GetPrivateField<T>(object target, string name) where T : class
        {
            FieldInfo field = target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
            return field == null ? null : field.GetValue(target) as T;
        }

        private static IEnumerator LoadMainScene()
        {
            if (GameBootstrap.Instance != null)
            {
                Object.Destroy(GameBootstrap.Instance.gameObject);
                yield return null;
            }

            SceneManager.LoadScene("Main");
            yield return null;
            yield return null;
            yield return null;
        }
    }
}
