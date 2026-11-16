using System.Collections;
using System.Reflection;
using Backgammon.Core;
using Backgammon.UI;
using NUnit.Framework;
using TavlaAI.Bootstrap;
using TavlaAI.Input;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace TavlaAI.Tests
{
    /// <summary>
    /// Dokuzuncu tur oynanis hatasinin Unity tarafindaki regresyon testleri.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Hata:</b> Siyah ile oynarken 6-4 zarinda 8 ve 6'dan Beyaz'in 2'deki tek tasi
    /// kirildi. Bar'a Beyaz tas gelmesi gerekirken 2'de tek bir Siyah tas gorundu ve toplam
    /// Siyah tas sayisi 14'e dustu.
    /// </para>
    /// <para>
    /// <b>Kok neden:</b> <see cref="MoveInputController"/> adimlari
    /// <see cref="LegalMoveGenerator.GenerateAllLegalMoves"/> ciktisinin <b>tum</b> adimlarini
    /// tarayarak kopyaliyordu. Uretici ayni son duruma ulasan dizileri tekillestirdigi icin bir
    /// dizinin <c>k &gt; 0</c> adimi <b>baska bir ara duruma</b> aittir; o adimin
    /// <see cref="MoveFlags.Hit"/> bayragi guncel duruma gore bayattir. Bayat <c>Hit</c> ile
    /// uygulanan adim, hedefteki <b>kendi</b> tasini siler.
    /// </para>
    /// <para>
    /// <b>Cozum:</b> adimlar guncel durumdan istenir
    /// (<see cref="LegalMoveGenerator.GenerateFirstSteps"/>).
    /// </para>
    /// </remarks>
    [TestFixture]
    public sealed class GameplayFix9PlayModeTests
    {
        [UnityTest]
        public IEnumerator HitThenStack_KeepsFifteenCheckersEach()
        {
            yield return LoadMainScene();

            GameBootstrap bootstrap = GameBootstrap.Instance;
            Assert.That(bootstrap, Is.Not.Null, "GameBootstrap kurulmadi");

            GameOrchestrator orchestrator = GetPrivateField<GameOrchestrator>(bootstrap, "_orchestrator");
            MoveInputController input = GetPrivateField<MoveInputController>(bootstrap, "_moveInput");
            BoardViewModel model = GetPrivateField<BoardViewModel>(bootstrap, "_boardModel");
            Assert.That(orchestrator, Is.Not.Null, "_orchestrator bulunamadi");
            Assert.That(input, Is.Not.Null, "_moveInput bulunamadi");
            Assert.That(model, Is.Not.Null, "_boardModel bulunamadi");

            Assert.That(bootstrap.HumanPlayer, Is.EqualTo(-1), "varsayilan renk Siyah olmali");

            orchestrator.NewGame();
            yield return null;

            BackgammonState state = orchestrator.State;
            SetProbePosition(state);
            state.phase = GamePhase.WaitForRoll;
            orchestrator.SetDice(6, 4);
            yield return null;

            state = orchestrator.State;
            Assert.That(state.turn, Is.EqualTo(-1), "Siyah sirada olmali");
            Assert.That(state.phase, Is.EqualTo(GamePhase.Moving), "faz Moving olmali");
            Assert.That(state.remainingCount, Is.EqualTo(2), "iki zar kalmali");
            Assert.That(state.board[2], Is.EqualTo(1), "2'de tek Beyaz tas olmali");
            Assert.That(state.board[0], Is.EqualTo(0), "bar bos olmali");

            MethodInfo click = typeof(BoardInputController).GetMethod(
                "OnPointClicked", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(click, Is.Not.Null, "OnPointClicked bulunamadi");

            // 1) 8'deki tasi sec: 2 hedef olmali (zar 6).
            click.Invoke(bootstrap.InputController, new object[] { 8 });
            Assert.That(input.SelectedPoint, Is.EqualTo(8), "8'deki tas secilemedi");
            Assert.That(input.LegalTargets, Does.Contain(2), "2 hedef olmali");

            // 2) 8 -> 2: Beyaz'in tek tasi kirilir.
            click.Invoke(bootstrap.InputController, new object[] { 2 });
            Assert.That(input.PartialMove.Count, Is.EqualTo(1), "ilk adim eklenmedi");
            Assert.That(input.SelectedPoint, Is.EqualTo(2), "secim yeni hanede kalmali");

            // 3) 6'daki tasi sec, sonra 2'ye oyna: kendi tasinin uzerine biner.
            click.Invoke(bootstrap.InputController, new object[] { 6 });
            Assert.That(input.SelectedPoint, Is.EqualTo(6), "6'daki tas secilemedi");
            Assert.That(input.LegalTargets, Does.Contain(2), "2 hedef olmali (zar 4)");

            click.Invoke(bootstrap.InputController, new object[] { 2 });
            yield return null;

            state = orchestrator.State;
            Assert.That(input.HasPartialMove, Is.False, "hamle tamamlanmadi");

            // Motor tarafi: 15/15 korunmali, Beyaz bar'da, 2'de iki Siyah tas.
            Assert.That(CountCheckers(state, 1), Is.EqualTo(15), "Beyaz tas sayisi 15 olmali");
            Assert.That(CountCheckers(state, -1), Is.EqualTo(15), "Siyah tas sayisi 15 olmali");
            Assert.That(state.board[0], Is.EqualTo(1), "kirilan Beyaz tas bar'a girmeli");
            Assert.That(state.board[2], Is.EqualTo(-2), "2'de iki Siyah tas olmali");
            Assert.That(state.board[8], Is.EqualTo(0), "8 bos olmali");
            Assert.That(state.board[6], Is.EqualTo(0), "6 bos olmali");

            // Gorsel taraf: modele yansiyan sayilar da 15/15 olmali.
            Assert.That(model.WhiteBar.Count, Is.EqualTo(1), "modelde Beyaz bar 1 olmali");
            Assert.That(model.PointAt(2).Count, Is.EqualTo(2), "modelde 2'de 2 tas olmali");
            Assert.That(model.PointAt(2).Owner, Is.EqualTo(-1), "modelde 2 Siyah olmali");
            Assert.That(TotalOnBoard(model, 1), Is.EqualTo(15), "modelde Beyaz toplam 15 olmali");
            Assert.That(TotalOnBoard(model, -1), Is.EqualTo(15), "modelde Siyah toplam 15 olmali");
        }

        [UnityTest]
        public IEnumerator ReverseOrder_KeepsFifteenCheckersEach()
        {
            yield return LoadMainScene();

            GameBootstrap bootstrap = GameBootstrap.Instance;
            Assert.That(bootstrap, Is.Not.Null, "GameBootstrap kurulmadi");

            GameOrchestrator orchestrator = GetPrivateField<GameOrchestrator>(bootstrap, "_orchestrator");
            MoveInputController input = GetPrivateField<MoveInputController>(bootstrap, "_moveInput");
            Assert.That(orchestrator, Is.Not.Null);
            Assert.That(input, Is.Not.Null);

            orchestrator.NewGame();
            yield return null;

            BackgammonState state = orchestrator.State;
            SetProbePosition(state);
            state.phase = GamePhase.WaitForRoll;
            orchestrator.SetDice(6, 4);
            yield return null;

            MethodInfo click = typeof(BoardInputController).GetMethod(
                "OnPointClicked", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(click, Is.Not.Null);

            // Once 6 -> 2 (zar 4, kiris), sonra 8 -> 2 (zar 6, biner).
            click.Invoke(bootstrap.InputController, new object[] { 6 });
            Assert.That(input.LegalTargets, Does.Contain(2), "2 hedef olmali (zar 4)");
            click.Invoke(bootstrap.InputController, new object[] { 2 });
            Assert.That(input.PartialMove.Count, Is.EqualTo(1));

            click.Invoke(bootstrap.InputController, new object[] { 8 });
            Assert.That(input.LegalTargets, Does.Contain(2), "2 hedef olmali (zar 6)");
            click.Invoke(bootstrap.InputController, new object[] { 2 });
            yield return null;

            state = orchestrator.State;
            Assert.That(CountCheckers(state, 1), Is.EqualTo(15), "Beyaz tas sayisi 15 olmali");
            Assert.That(CountCheckers(state, -1), Is.EqualTo(15), "Siyah tas sayisi 15 olmali");
            Assert.That(state.board[0], Is.EqualTo(1), "kirilan Beyaz tas bar'a girmeli");
            Assert.That(state.board[2], Is.EqualTo(-2), "2'de iki Siyah tas olmali");
        }

        // ------------------------------------------------------------------
        // Yardimcilar
        // ------------------------------------------------------------------

        /// <summary>
        /// Bildirilen pozisyon: Siyah 24x5, 20x3, 13x5, 8x1, 6x1; Beyaz 19x2, 17x5, 12x5, 2x1, 1x2.
        /// Siyah 6-4 ile 8 -&gt; 2 ve 6 -&gt; 2 oynayarak 2'deki tek Beyaz tasi kirar.
        /// </summary>
        private static void SetProbePosition(BackgammonState state)
        {
            for (int i = 0; i < BackgammonState.BoardSize; i++)
            {
                state.board[i] = 0;
            }

            state.board[24] = -5;
            state.board[20] = -3;
            state.board[13] = -5;
            state.board[8] = -1;
            state.board[6] = -1;

            state.board[19] = 2;
            state.board[17] = 5;
            state.board[12] = 5;
            state.board[2] = 1;
            state.board[1] = 2;

            state.whiteBorneOff = 0;
            state.blackBorneOff = 0;
            state.turn = -1;
            state.hash = BackgammonEngine.ComputeZobristHash(state);
        }

        private static int CountCheckers(BackgammonState s, int player)
        {
            int total = player == 1 ? s.whiteBorneOff : s.blackBorneOff;
            for (int p = 0; p < BackgammonState.BoardSize; p++)
            {
                int v = s.board[p];
                if (player == 1 ? v > 0 : v < 0)
                {
                    total += Mathf.Abs(v);
                }
            }

            return total;
        }

        private static int TotalOnBoard(BoardViewModel model, int player)
        {
            int total = player == 1 ? model.WhiteBar.Count : model.BlackBar.Count;
            for (int p = 1; p <= 24; p++)
            {
                PointView data = model.PointAt(p);
                if (data.Owner == player)
                {
                    total += data.Count;
                }
            }

            return total;
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
