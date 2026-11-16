using System.Collections;
using System.Reflection;
using Backgammon.Core;
using Backgammon.UI;
using NUnit.Framework;
using TavlaAI.Bootstrap;
using TavlaAI.Dice;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace TavlaAI.Tests
{
    /// <summary>
    /// Oynanis hatalarinin Unity tarafindaki regresyon testleri: vurulan tas bara gider,
    /// toplanan taslar tepside cizilir, rakip zari gorunur kalir.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Neden <c>VisibleCount == 30</c>?</b> Toplam tas sayisi her zaman 15 + 15 = 30'dur.
    /// Vurulan tas bara gitmezse (bayrak kaybi) veya toplanan tas tepsiye cizilmezse havuz
    /// 29 tas cizer. Tek bir sayim, her iki hatayi da yakalar.
    /// </para>
    /// </remarks>
    [TestFixture]
    public sealed class GameplayFixPlayModeTests
    {
        [UnityTest]
        public IEnumerator Hit_SendsOpponentCheckerToBar_AndRenders()
        {
            yield return LoadMainScene();

            GameBootstrap bootstrap = GameBootstrap.Instance;
            Assert.That(bootstrap, Is.Not.Null, "GameBootstrap kurulmadi");

            GameOrchestrator orchestrator = GetPrivateField<GameOrchestrator>(bootstrap, "_orchestrator");
            BoardViewModel model = GetPrivateField<BoardViewModel>(bootstrap, "_boardModel");
            Assert.That(orchestrator, Is.Not.Null);
            Assert.That(model, Is.Not.Null);

            // Insan varsayilan olarak Siyah oynar; test her iki renkte de calismali.
            sbyte human = bootstrap.HumanPlayer;
            int from = human > 0 ? 1 : 24;
            int to = human > 0 ? 4 : 21;
            int humanHome = human > 0 ? 2 : 23;
            int oppHome = human > 0 ? 6 : 19;

            SetPosition(orchestrator, human, new[] { from, humanHome }, new[] { oppHome, to });
            orchestrator.SetDice(3, 5);
            yield return null;

            Assert.That(model.PointAt(to).Owner, Is.EqualTo((sbyte)-human), "hedefte rakip blot olmali");

            MethodInfo click = typeof(TavlaAI.Input.BoardInputController).GetMethod(
                "OnPointClicked", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(click, Is.Not.Null);

            click.Invoke(bootstrap.InputController, new object[] { from });
            click.Invoke(bootstrap.InputController, new object[] { to });
            bootstrap.InputController.CommitPartialMove();
            yield return null;

            Assert.That(orchestrator.State.board[to], Is.EqualTo(human), "vuran tas hedefte kalmali");
            Assert.That(orchestrator.State.board[BackgammonEngine.BarIndex((sbyte)-human)],
                        Is.EqualTo((sbyte)-human), "kirilan tas rakip barina gitmeli");
            Assert.That(model.BlackBar.Count + model.WhiteBar.Count, Is.EqualTo(1), "bar gorunumu guncellenmeli");
            Assert.That(bootstrap.BoardView.Pool.VisibleCount, Is.EqualTo(30),
                        "tas sayisi korunmali (vurulan tas silinmemeli)");
        }

        [UnityTest]
        public IEnumerator BearOff_DrawsCheckersInTray()
        {
            yield return LoadMainScene();

            GameBootstrap bootstrap = GameBootstrap.Instance;
            Assert.That(bootstrap, Is.Not.Null, "GameBootstrap kurulmadi");

            GameOrchestrator orchestrator = GetPrivateField<GameOrchestrator>(bootstrap, "_orchestrator");
            BoardViewModel model = GetPrivateField<BoardViewModel>(bootstrap, "_boardModel");
            MoveInputController input = GetPrivateField<MoveInputController>(bootstrap, "_moveInput");

            sbyte human = bootstrap.HumanPlayer;
            int home = human > 0 ? 24 : 1;
            int tray = human > 0 ? 25 : 0;
            int oppHome = human > 0 ? 1 : 24;

            SetPosition(orchestrator, human, new[] { home }, new[] { oppHome });
            orchestrator.SetDice(1, 1);
            yield return null;

            Assert.That(input.LegalTargetsFrom(home), Does.Contain(tray), "toplama yasal olmali");

            input.SelectPoint(home);
            Assert.That(input.TryAddStep(tray), Is.True);
            bootstrap.InputController.CommitPartialMove();
            yield return null;

            int borneOff = human > 0 ? orchestrator.State.whiteBorneOff : orchestrator.State.blackBorneOff;
            int modelBorneOff = human > 0 ? model.WhiteBorneOff : model.BlackBorneOff;
            Assert.That(borneOff, Is.EqualTo(1), "toplanan tas sayaci artmali");
            Assert.That(modelBorneOff, Is.EqualTo(1), "tepsi gorunumu guncellenmeli");
            Assert.That(bootstrap.BoardView.Pool.VisibleCount, Is.EqualTo(30),
                        "toplanan tas tepside cizilmeli (29 degil 30)");
        }

        [UnityTest]
        public IEnumerator DiceView_ShowsOverrideWhenBoardHasNoDice()
        {
            yield return LoadMainScene();

            GameBootstrap bootstrap = GameBootstrap.Instance;
            Assert.That(bootstrap, Is.Not.Null, "GameBootstrap kurulmadi");

            GameOrchestrator orchestrator = GetPrivateField<GameOrchestrator>(bootstrap, "_orchestrator");
            DiceView dice = bootstrap.DiceView;
            Assert.That(dice, Is.Not.Null);

            // Kare beklemeden ilerliyoruz: bir sonraki Update acilis zarini atardi.
            orchestrator.NewGame();

            Assert.That(orchestrator.State.rolledDice[0], Is.EqualTo(0), "tahtada zar yok");

            dice.SetOverride(5, 2, "rakip: 5-2");
            dice.Refresh(GetPrivateField<BoardViewModel>(bootstrap, "_boardModel"));

            Assert.That(dice.Die1Image.enabled, Is.True, "sabitlenen zar cizilmeli");
            Assert.That(dice.Die2Image.enabled, Is.True);
            Assert.That(dice.Die1Image.sprite, Is.Not.Null);
            Assert.That(dice.RemainingLabel.text, Is.EqualTo("rakip: 5-2"));

            dice.ClearOverride();
            dice.Refresh(GetPrivateField<BoardViewModel>(bootstrap, "_boardModel"));

            Assert.That(dice.Die1Image.enabled, Is.False, "sabitleme kaldirilinca zar gizlenmeli");
        }

        // ------------------------------------------------------------------
        // Yardimcilar
        // ------------------------------------------------------------------

        /// <summary>
        /// Tahtayi sifirlar ve verilen hanelere 15'er tas koyar (ilk hane 15 tasi alir).
        /// </summary>
        /// <param name="orchestrator">Orkestrator.</param>
        /// <param name="human">Insan tarafi; <c>+1</c> ise ilk dizi Beyaz, <c>-1</c> ise Siyah olur.</param>
        /// <param name="humanPoints">Insan taslarinin haneleri.</param>
        /// <param name="opponentPoints">Rakip taslarinin haneleri.</param>
        private static void SetPosition(GameOrchestrator orchestrator, sbyte human,
                                       int[] humanPoints, int[] opponentPoints)
        {
            int[] white = human > 0 ? humanPoints : opponentPoints;
            int[] black = human > 0 ? opponentPoints : humanPoints;

            BackgammonState state = orchestrator.State;
            for (int i = 0; i < BackgammonState.BoardSize; i++)
            {
                state.board[i] = 0;
            }

            state.board[white[0]] = (sbyte)(15 - (white.Length - 1));
            for (int i = 1; i < white.Length; i++)
            {
                state.board[white[i]] = 1;
            }

            state.board[black[0]] = (sbyte)-(15 - (black.Length - 1));
            for (int i = 1; i < black.Length; i++)
            {
                state.board[black[i]] = -1;
            }

            state.whiteBorneOff = 0;
            state.blackBorneOff = 0;
            state.turn = human;
            state.phase = GamePhase.WaitForRoll;
            state.hash = BackgammonEngine.ComputeZobristHash(state);
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
