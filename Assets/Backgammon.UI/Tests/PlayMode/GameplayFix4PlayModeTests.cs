using System.Collections;
using System.Reflection;
using Backgammon.Core;
using Backgammon.UI;
using NUnit.Framework;
using TavlaAI.Board;
using TavlaAI.Bootstrap;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace TavlaAI.Tests
{
    /// <summary>
    /// Dorduncu tur oynanis hatalarinin Unity tarafindaki regresyon testleri.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Hata 1:</b> Siyah oynarken ev bolgesinde (toplama bolgesi) bir Beyaz tas vuruldugunda
    /// tahtadan bir tas <b>eksiliyordu</b>. Vurulan tasin bara gitmesi ve toplam tas sayisinin
    /// <c>30</c> kalması gerekir.
    /// </para>
    /// <para>
    /// <b>Hata 2:</b> Insan oyunu kazandiginda oyun bitmeli, skor <b>aninda</b> guncellenmeli ve
    /// yapay zeka <b>bir daha hamle yapmamalidir</b>.
    /// </para>
    /// <para>
    /// <b>Hata 3:</b> Insan hangi rengi secerse secsin kendi toplama tepsisi <b>sag altta</b>
    /// olmalidir; hane yerlesimi degismez.
    /// </para>
    /// </remarks>
    [TestFixture]
    public sealed class GameplayFix4PlayModeTests
    {
        // ------------------------------------------------------------------
        // Hata 1: ev bolgesinde vurulan tas kaybolmamali
        // ------------------------------------------------------------------

        [UnityTest]
        public IEnumerator Hit_InBearOffArea_KeepsAllThirtyCheckers()
        {
            yield return LoadMainScene();

            GameBootstrap bootstrap = GameBootstrap.Instance;
            Assert.That(bootstrap, Is.Not.Null, "GameBootstrap kurulmadi");

            GameOrchestrator orchestrator = GetPrivateField<GameOrchestrator>(bootstrap, "_orchestrator");
            BoardViewModel model = GetPrivateField<BoardViewModel>(bootstrap, "_boardModel");
            MoveInputController input = GetPrivateField<MoveInputController>(bootstrap, "_moveInput");
            Assert.That(orchestrator, Is.Not.Null);
            Assert.That(model, Is.Not.Null);
            Assert.That(input, Is.Not.Null);

            // Insan varsayilan olarak Siyah oynar; test her iki renkte de calisir.
            sbyte human = bootstrap.HumanPlayer;
            int home = human > 0 ? 24 : 1;      // insanin ev bolgesi (toplama bolgesi)
            int from = human > 0 ? 19 : 6;      // vuran tas
            int blot = human > 0 ? 21 : 4;      // ev bolgesindeki rakip blot
            int oppStack = human > 0 ? 12 : 13; // rakibin kalan taslari
            int die = Mathf.Abs(blot - from);

            SetPosition(orchestrator, human, new[] { home, from }, new[] { oppStack, blot });
            orchestrator.SetDice(die, die);
            yield return null;

            Assert.That(model.PointAt(blot).Owner, Is.EqualTo((sbyte)-human), "hedefte rakip blot olmali");
            Assert.That(input.LegalTargetsFrom(from), Does.Contain(blot), "vurma yasal olmali");

            input.SelectPoint(from);
            Assert.That(input.TryAddStep(blot), Is.True);
            bootstrap.InputController.CommitPartialMove();
            yield return null;

            Assert.That(orchestrator.State.board[blot], Is.EqualTo(human), "vuran tas hedefte kalmali");
            Assert.That(orchestrator.State.board[BackgammonEngine.BarIndex((sbyte)-human)],
                        Is.EqualTo((sbyte)-human), "vurulan tas rakip barina gitmeli");
            Assert.That(orchestrator.State.CountPieces(1), Is.EqualTo(15), "Beyaz tas sayisi korunmali");
            Assert.That(orchestrator.State.CountPieces(-1), Is.EqualTo(15), "Siyah tas sayisi korunmali");
            Assert.That(model.WhiteBar.Count + model.BlackBar.Count, Is.EqualTo(1),
                        "bar gorunumu guncellenmeli");
            Assert.That(bootstrap.BoardView.Pool.VisibleCount, Is.EqualTo(30),
                        "tas sayisi korunmali (vurulan tas silinmemeli)");

            // Vurulan tas bar'da <b>gorunur</b> olmali: her tas tahta sinirlari icinde ve
            // sifirdan buyuk boyutta cizilmis olmali.
            AssertAllCheckersInsideBoard(bootstrap.BoardView);
        }

        // ------------------------------------------------------------------
        // Hata 2: insan kazaninca oyun biter, skor guncellenir, AI oynamaz
        // ------------------------------------------------------------------

        [UnityTest]
        public IEnumerator HumanWin_EndsGame_UpdatesScore_AndStopsAi()
        {
            yield return LoadMainScene();

            GameBootstrap bootstrap = GameBootstrap.Instance;
            Assert.That(bootstrap, Is.Not.Null, "GameBootstrap kurulmadi");

            GameOrchestrator orchestrator = GetPrivateField<GameOrchestrator>(bootstrap, "_orchestrator");
            MoveInputController input = GetPrivateField<MoveInputController>(bootstrap, "_moveInput");
            Assert.That(orchestrator, Is.Not.Null);
            Assert.That(input, Is.Not.Null);

            sbyte human = bootstrap.HumanPlayer;
            int home = human > 0 ? 24 : 1;
            int tray = human > 0 ? 25 : 0;

            SetLastCheckerPosition(orchestrator, human);
            orchestrator.SetDice(1, 1);
            yield return null;

            int scoreBefore = human > 0 ? orchestrator.State.whiteScore : orchestrator.State.blackScore;

            Assert.That(input.LegalTargetsFrom(home), Does.Contain(tray), "toplama yasal olmali");
            input.SelectPoint(home);
            Assert.That(input.TryAddStep(tray), Is.True);
            bootstrap.InputController.CommitPartialMove();
            yield return null;

            int scoreAfter = human > 0 ? orchestrator.State.whiteScore : orchestrator.State.blackScore;
            sbyte turnAtGameOver = orchestrator.State.turn;
            byte die1AtGameOver = orchestrator.State.rolledDice[0];
            byte die2AtGameOver = orchestrator.State.rolledDice[1];

            Assert.That(orchestrator.IsGameOver, Is.True, "oyun bitmis olmali");
            Assert.That(orchestrator.State.phase, Is.EqualTo(GamePhase.GameOver), "faz GameOver olmali");
            Assert.That(scoreAfter, Is.GreaterThan(scoreBefore), "skor guncellenmeli");
            Assert.That(orchestrator.Result.Winner, Is.EqualTo(human), "kazanan insan olmali");

            // Birak kac kare gecsin: AI sirasi gelmemeli, zar atilmamali.
            for (int i = 0; i < 10; i++)
            {
                yield return null;
            }

            Assert.That(orchestrator.State.turn, Is.EqualTo(turnAtGameOver),
                        "oyun bittikten sonra sira degismemeli (AI hamle yapmamali)");
            Assert.That(orchestrator.State.rolledDice[0], Is.EqualTo(die1AtGameOver), "AI zar atmamali");
            Assert.That(orchestrator.State.rolledDice[1], Is.EqualTo(die2AtGameOver), "AI zar atmamali");
            Assert.That(orchestrator.State.phase, Is.EqualTo(GamePhase.GameOver), "faz GameOver kalmali");
            Assert.That(human > 0 ? orchestrator.State.whiteScore : orchestrator.State.blackScore,
                        Is.EqualTo(scoreAfter), "skor cift sayilmamali");
        }

        // ------------------------------------------------------------------
        // Hata 3: insanin toplama tepsisi her renkte sag altta
        // ------------------------------------------------------------------

        [UnityTest]
        public IEnumerator HumanTray_IsBottomRight_ForBothColors()
        {
            yield return LoadMainScene();

            GameBootstrap bootstrap = GameBootstrap.Instance;
            Assert.That(bootstrap, Is.Not.Null, "GameBootstrap kurulmadi");

            BoardView board = bootstrap.BoardView;
            Assert.That(board, Is.Not.Null);

            // Varsayilan renk Siyah: Siyah tepsisi alt yarida olmali.
            Assert.That(bootstrap.HumanPlayer, Is.EqualTo((sbyte)-1), "varsayilan renk Siyah olmali");
            Assert.That(board.HumanTrayBottom, Is.True, "Siyah oynarken tepsi alt yarida olmali");
            Assert.That(board.FlipPoints, Is.False, "Siyah oynarken tahta aynalanmamali");
            AssertTrayIsBottom(board, -1);
            AssertHomeBoardIsBottomRight(board, -1);

            // Beyaza gec: Beyaz tepsisi alt yariya, Siyah tepsisi ust yariya tasinmali.
            SetPlayerColor(bootstrap, 1);
            yield return null;

            Assert.That(bootstrap.HumanPlayer, Is.EqualTo((sbyte)1), "renk Beyaz olmali");
            Assert.That(board.HumanTrayBottom, Is.False, "Beyaz oynarken tepsi alt yarida olmali");
            Assert.That(board.FlipPoints, Is.True, "Beyaz oynarken tahta aynalanmali");
            AssertTrayIsBottom(board, 1);
            AssertHomeBoardIsBottomRight(board, 1);
        }

        /// <summary>
        /// Insanin ev bolgesinin (toplama bolgesinin) her renkte <b>sag altta</b> oldugunu
        /// dogrular. Beyaz 19..24, Siyah 1..6 hanelerinde toplar.
        /// </summary>
        private static void AssertHomeBoardIsBottomRight(BoardView board, sbyte human)
        {
            int first = human > 0 ? 19 : 1;
            int last = human > 0 ? 24 : 6;

            float barRight = ((RectTransform)board.WhiteBar.transform).anchoredPosition.x;
            float halfHeight = ((RectTransform)board.transform).rect.height * 0.5f;

            for (int point = first; point <= last; point++)
            {
                Vector2 position = board.Points[point - 1].Rect.anchoredPosition;
                Assert.That(position.y, Is.LessThanOrEqualTo(-halfHeight + 1f),
                            "hane " + point + " alt satirda olmali (y=" + position.y + ")");
                Assert.That(position.x, Is.GreaterThan(barRight),
                            "hane " + point + " sag yarida olmali (x=" + position.x + ")");
            }
        }

        // ------------------------------------------------------------------
        // Yardimcilar
        // ------------------------------------------------------------------

        private static void AssertTrayIsBottom(BoardView board, sbyte human)
        {
            TrayView mine = human > 0 ? board.WhiteTray : board.BlackTray;
            TrayView theirs = human > 0 ? board.BlackTray : board.WhiteTray;
            Assert.That(mine, Is.Not.Null);
            Assert.That(theirs, Is.Not.Null);

            var mineRect = (RectTransform)mine.transform;
            var theirsRect = (RectTransform)theirs.transform;

            Assert.That(mineRect.anchoredPosition.y, Is.LessThan(theirsRect.anchoredPosition.y),
                        "insanin tepsisi alt yarida (daha negatif y) olmali");
            Assert.That(mineRect.anchoredPosition.x, Is.GreaterThan(0f), "tepsi sag tarafta olmali");
        }

        /// <summary>
        /// Havuzdaki her gorunur tasin tahta sinirlari icinde ve sifirdan buyuk boyutta
        /// cizildigini dogrular. Tas "kayboldu" gorunumunun kaynagi burada yakalanir.
        /// </summary>
        private static void AssertAllCheckersInsideBoard(BoardView board)
        {
            var root = (RectTransform)board.transform;
            float width = root.rect.width;
            float height = root.rect.height;
            Assert.That(width, Is.GreaterThan(0f));
            Assert.That(height, Is.GreaterThan(0f));

            CheckerPool pool = board.Pool;
            for (int i = 0; i < pool.VisibleCount; i++)
            {
                CheckerView checker = pool[i];
                Vector2 position = checker.Rect.anchoredPosition;
                Vector2 size = checker.Rect.sizeDelta;

                Assert.That(size.x, Is.GreaterThan(1f), "tas #{0} boyutu sifir olmamali", i);
                Assert.That(position.x, Is.InRange(-1f, width + 1f), "tas #{0} yatayda tahta icinde olmali", i);
                Assert.That(position.y, Is.InRange(-height - 1f, 1f), "tas #{0} dikeyde tahta icinde olmali", i);
            }
        }

        private static void SetPlayerColor(GameBootstrap bootstrap, sbyte color)
        {
            MethodInfo apply = typeof(GameBootstrap).GetMethod(
                "ApplyPlayerColor", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(apply, Is.Not.Null);
            apply.Invoke(bootstrap, new object[] { color, true });
        }

        /// <summary>
        /// Tahtayi sifirlar ve verilen hanelere 15'er tas koyar (ilk hane kalan taslari alir).
        /// </summary>
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

        /// <summary>Insanin son tasini ev bolgesine koyar; 14 tas toplanmis durumdadir.</summary>
        private static void SetLastCheckerPosition(GameOrchestrator orchestrator, sbyte human)
        {
            BackgammonState state = orchestrator.State;
            for (int i = 0; i < BackgammonState.BoardSize; i++)
            {
                state.board[i] = 0;
            }

            int home = human > 0 ? 24 : 1;
            int oppHome = human > 0 ? 1 : 24;

            state.board[home] = human;
            state.board[oppHome] = (sbyte)(-human * 15);

            state.whiteBorneOff = (byte)(human > 0 ? 14 : 0);
            state.blackBorneOff = (byte)(human > 0 ? 0 : 14);
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
