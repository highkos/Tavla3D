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
    /// Renk degistirme (bakis acisi) yamasinin Unity tarafindaki regresyon testleri.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Istek:</b> "Renk degistir secenegi tercih edildiginde (varsayilan Siyah) Beyaz renk
    /// siyah, siyah renk beyaz olsun; yani taslari oyuncu her iki renkte de ayni yone hareket
    /// etmis olsun."
    /// </para>
    /// <para>
    /// <b>Cozum:</b> oyuncu Beyaz oynarken hane numaralari 180 derece dondurulur
    /// (<see cref="BoardView.FlipPoints"/>). Boylece oyuncunun ev bolgesi ve hareket yonu her
    /// iki renkte de ayni kalir. Tahta cercevesi, bar ve tepsi seridi <b>yer degistirmez</b>.
    /// </para>
    /// </remarks>
    [TestFixture]
    public sealed class GameplayFix10PlayModeTests
    {
        [UnityTest]
        public IEnumerator FlipPoints_MirrorsPointRects_AndKeepsFrame()
        {
            yield return LoadMainScene();

            GameBootstrap bootstrap = GameBootstrap.Instance;
            Assert.That(bootstrap, Is.Not.Null, "GameBootstrap kurulmadi");

            BoardView board = bootstrap.BoardView;
            Assert.That(board, Is.Not.Null, "BoardView yok");

            bool originalFlip = board.FlipPoints;
            try
            {
                board.FlipPoints = false;
                board.Refresh();
                Vector2 point1Plain = board.Points[0].Rect.anchoredPosition;
                Vector2 point24Plain = board.Points[23].Rect.anchoredPosition;
                Vector2 whiteBarPlain = ((RectTransform)board.WhiteBar.transform).anchoredPosition;
                Vector2 blackBarPlain = ((RectTransform)board.BlackBar.transform).anchoredPosition;
                Vector2 trayPlain = ((RectTransform)board.WhiteTray.transform).anchoredPosition;

                board.FlipPoints = true;
                board.Refresh();

                Assert.That(board.Points[0].Rect.anchoredPosition, Is.EqualTo(point24Plain),
                            "aynalamada hane 1, hane 24'un yerine gecmeli");
                Assert.That(board.Points[23].Rect.anchoredPosition, Is.EqualTo(point1Plain),
                            "aynalamada hane 24, hane 1'in yerine gecmeli");

                // Bar seridi yerinde kalir; yalnizca yarimlar ev bolgelerini izlemek icin takas olur.
                Assert.That(((RectTransform)board.WhiteBar.transform).anchoredPosition,
                            Is.EqualTo(blackBarPlain), "beyaz bar alt yarima tasinmali");
                Assert.That(((RectTransform)board.BlackBar.transform).anchoredPosition,
                            Is.EqualTo(whiteBarPlain), "siyah bar ust yarima tasinmali");

                // Tepsi yarimlari HumanTrayBottom'a baglidir; aynalama onlari degistirmez.
                Assert.That(((RectTransform)board.WhiteTray.transform).anchoredPosition,
                            Is.EqualTo(trayPlain), "tepsi yeri degismemeli");
            }
            finally
            {
                board.FlipPoints = originalFlip;
                board.Refresh();
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator FlipPoints_KeepsCheckersInsideTheirPoints()
        {
            yield return LoadMainScene();

            GameBootstrap bootstrap = GameBootstrap.Instance;
            Assert.That(bootstrap, Is.Not.Null, "GameBootstrap kurulmadi");

            BoardView board = bootstrap.BoardView;
            BoardViewModel model = GetPrivateField<BoardViewModel>(bootstrap, "_boardModel");
            Assert.That(board, Is.Not.Null, "BoardView yok");
            Assert.That(model, Is.Not.Null, "BoardViewModel yok");

            var state = new BackgammonState();
            state.Reset();
            model.Update(state);

            bool originalFlip = board.FlipPoints;
            try
            {
                foreach (bool flip in new[] { false, true })
                {
                    board.FlipPoints = flip;
                    board.Refresh();
                    AssertCheckersInsidePoints(board, model, "flip=" + flip);
                }
            }
            finally
            {
                board.FlipPoints = originalFlip;
                board.Refresh();
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator HomeBoards_KeepTheirScreenPositions_ForBothColors()
        {
            yield return LoadMainScene();

            GameBootstrap bootstrap = GameBootstrap.Instance;
            Assert.That(bootstrap, Is.Not.Null, "GameBootstrap kurulmadi");

            BoardView board = bootstrap.BoardView;
            Assert.That(board, Is.Not.Null, "BoardView yok");

            sbyte original = bootstrap.HumanPlayer;
            try
            {
                foreach (sbyte color in new[] { (sbyte)-1, (sbyte)1 })
                {
                    SetPlayerColor(bootstrap, color);
                    Assert.That(bootstrap.HumanPlayer, Is.EqualTo(color));

                    // Insanin ev bolgesi her zaman sag altta, rakibinki sag ustte.
                    int humanFirst = color > 0 ? 19 : 1;
                    int humanLast = color > 0 ? 24 : 6;
                    int opponentFirst = color > 0 ? 1 : 19;
                    int opponentLast = color > 0 ? 6 : 24;

                    AssertHomeBoard(board, humanFirst, humanLast, bottom: true,
                                    "renk " + color + ": insanin ev bolgesi");
                    AssertHomeBoard(board, opponentFirst, opponentLast, bottom: false,
                                    "renk " + color + ": rakibin ev bolgesi");

                    // Toplama kenari (ev bolgesinin hemen otesi) da sag altta olmali.
                    int edge = color > 0 ? 24 : 1;
                    AssertHomeBoard(board, edge, edge, bottom: true,
                                    "renk " + color + ": toplama kenari");
                }
            }
            finally
            {
                SetPlayerColor(bootstrap, original);
            }

            yield return null;
        }

        // ------------------------------------------------------------------
        // Yardimcilar
        // ------------------------------------------------------------------

        private static void AssertHomeBoard(BoardView board, int first, int last, bool bottom,
                                            string label)
        {
            float barX = ((RectTransform)board.WhiteBar.transform).anchoredPosition.x;
            float halfHeight = ((RectTransform)board.transform).rect.height * 0.5f;

            for (int point = first; point <= last; point++)
            {
                Vector2 position = board.Points[point - 1].Rect.anchoredPosition;

                if (bottom)
                {
                    Assert.That(position.y, Is.LessThanOrEqualTo(-halfHeight + 1f),
                                label + ": hane " + point + " alt satirda olmali (y=" + position.y + ")");
                }
                else
                {
                    Assert.That(position.y, Is.GreaterThanOrEqualTo(-1f),
                                label + ": hane " + point + " ust satirda olmali (y=" + position.y + ")");
                }

                Assert.That(position.x, Is.GreaterThan(barX),
                            label + ": hane " + point + " sag yarida olmali (x=" + position.x + ")");
            }
        }

        private static void AssertCheckersInsidePoints(BoardView board, BoardViewModel model,
                                                       string label)
        {
            Assert.That(board.Pool.VisibleCount, Is.GreaterThan(0), label + ": hic pul cizilmedi");

            for (int point = 1; point <= BoardViewModel.PointCount; point++)
            {
                PointView data = model.PointAt(point);
                Rect rect = InsetRelative(WorldRect(board.Points[point - 1].Rect), 0.02f);

                int inside = 0;
                for (int i = 0; i < board.Pool.VisibleCount; i++)
                {
                    if (rect.Contains(WorldRect(board.Pool[i].Rect).center))
                    {
                        inside++;
                    }
                }

                Assert.That(inside, Is.EqualTo(data.Count),
                            label + ": hane " + point + " " + data.Count + " pul barindirmali, " +
                            "bulunan " + inside);
            }
        }

        private static Rect InsetRelative(Rect rect, float fraction)
        {
            float amount = Mathf.Min(rect.width, rect.height) * fraction;
            return new Rect(rect.x + amount, rect.y + amount,
                            Mathf.Max(0f, rect.width - (amount * 2f)),
                            Mathf.Max(0f, rect.height - (amount * 2f)));
        }

        private static Rect WorldRect(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return new Rect(corners[0].x, corners[0].y,
                            corners[2].x - corners[0].x, corners[2].y - corners[0].y);
        }

        private static void SetPlayerColor(GameBootstrap bootstrap, sbyte color)
        {
            MethodInfo apply = typeof(GameBootstrap).GetMethod(
                "ApplyPlayerColor", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(apply, Is.Not.Null, "ApplyPlayerColor bulunamadi");
            apply.Invoke(bootstrap, new object[] { color, true });
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
