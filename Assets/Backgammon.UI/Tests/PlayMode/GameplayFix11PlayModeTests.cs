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
    /// Orta bant (ust ve alt ucgen uclari arasindaki bosluk) Unity regresyon testleri.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Istek:</b> "Tavla tahtasindaki asagidaki ucgenin uc noktasi ile yukaridaki ucgenin
    /// uc noktalari birbirine cok yakin; gercek bir tavla tahtasinda ust ucgen ile alt ucgen
    /// arasinda bir miktar mesafe olmali. (Belki ileride logo vs koyabilirim orta alana.)"
    /// </para>
    /// <para>
    /// <b>Cozum:</b> <see cref="BoardLayout"/> iki hane satirini
    /// <see cref="BoardLayout.CenterGapRatio"/> kadar ayirir. Bu test <b>gercekten cizilen</b>
    /// <see cref="BoardPointView.Rect"/> dikdortgenlerini olcer; yani bosluk yerlesimde
    /// hesaplanmis olmakla kalmaz, sahneye de dogru aktarilir.
    /// </para>
    /// </remarks>
    [TestFixture]
    public sealed class GameplayFix11PlayModeTests
    {
        [UnityTest]
        public IEnumerator Triangles_DoNotTouchAtTheCenter()
        {
            yield return LoadMainScene();

            GameBootstrap bootstrap = GameBootstrap.Instance;
            Assert.That(bootstrap, Is.Not.Null, "GameBootstrap kurulmadi");

            BoardView board = bootstrap.BoardView;
            Assert.That(board, Is.Not.Null, "BoardView yok");

            float height = ((RectTransform)board.transform).rect.height;
            Assert.That(height, Is.GreaterThan(0f), "tahta yuksekligi");

            float topRowBottom = 0f;
            float bottomRowTop = float.MaxValue;

            for (int point = 1; point <= BoardLayout.PointCount; point++)
            {
                RectTransform rect = board.Points[point - 1].Rect;
                float top = -rect.anchoredPosition.y;
                float bottom = top + rect.sizeDelta.y;

                if (rect.anchoredPosition.y > -1f)
                {
                    // Ust satir: y = 0, ucgen asagi bakar.
                    topRowBottom = Mathf.Max(topRowBottom, bottom);
                }
                else
                {
                    // Alt satir: ucgen yukari bakar.
                    bottomRowTop = Mathf.Min(bottomRowTop, top);
                }
            }

            float gap = bottomRowTop - topRowBottom;
            float expected = height * BoardLayout.CenterGapRatio;

            Assert.That(gap, Is.GreaterThan(0f),
                        "ust ve alt ucgen uclari arasinda bosluk olmali (olculen " + gap + ")");
            Assert.That(gap, Is.EqualTo(expected).Within(1f),
                        "bosluk tahta yuksekliginin " + BoardLayout.CenterGapRatio +
                        " kati olmali (olculen " + gap + ", beklenen " + expected + ")");

            yield return null;
        }

        [UnityTest]
        public IEnumerator CenterGap_IsTheSameForBothColors()
        {
            yield return LoadMainScene();

            GameBootstrap bootstrap = GameBootstrap.Instance;
            Assert.That(bootstrap, Is.Not.Null, "GameBootstrap kurulmadi");

            BoardView board = bootstrap.BoardView;
            Assert.That(board, Is.Not.Null, "BoardView yok");

            sbyte original = bootstrap.HumanPlayer;
            float blackGap = 0f;
            try
            {
                foreach (sbyte color in new[] { (sbyte)-1, (sbyte)1 })
                {
                    SetPlayerColor(bootstrap, color);
                    float gap = MeasureCenterGap(board);

                    if (color < 0)
                    {
                        blackGap = gap;
                    }
                    else
                    {
                        Assert.That(gap, Is.EqualTo(blackGap).Within(0.5f),
                                    "aynalama orta banti degistirmemeli (siyah " + blackGap +
                                    ", beyaz " + gap + ")");
                    }
                }
            }
            finally
            {
                SetPlayerColor(bootstrap, original);
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator CenterGap_KeepsBarAndTrayFullHeight()
        {
            yield return LoadMainScene();

            GameBootstrap bootstrap = GameBootstrap.Instance;
            Assert.That(bootstrap, Is.Not.Null, "GameBootstrap kurulmadi");

            BoardView board = bootstrap.BoardView;
            Assert.That(board, Is.Not.Null, "BoardView yok");

            float height = ((RectTransform)board.transform).rect.height;

            // Bar ve tepsi dikey olarak kesintisizdir; orta bant onlari bolmez. Bar ve tepsi
            // iki yarimdan olusur (Beyaz/Siyah); yarimlar birlesince tam yukseklik kaplanmali.
            AssertHalvesCoverFullHeight((RectTransform)board.WhiteBar.transform,
                                        (RectTransform)board.BlackBar.transform, height, "bar");
            AssertHalvesCoverFullHeight((RectTransform)board.WhiteTray.transform,
                                        (RectTransform)board.BlackTray.transform, height, "tepsi");

            yield return null;
        }

        [UnityTest]
        public IEnumerator CenterGap_StaysEmptyOfCheckers()
        {
            yield return LoadMainScene();

            GameBootstrap bootstrap = GameBootstrap.Instance;
            Assert.That(bootstrap, Is.Not.Null, "GameBootstrap kurulmadi");

            BoardView board = bootstrap.BoardView;
            BoardViewModel model = GetPrivateField<BoardViewModel>(bootstrap, "_boardModel");
            Assert.That(board, Is.Not.Null, "BoardView yok");
            Assert.That(model, Is.Not.Null, "BoardViewModel yok");

            // Acilis dizilimi: 15 + 15 pul, iki satirda da yiginlar var.
            var state = new BackgammonState();
            state.Reset();
            model.Update(state);
            board.Refresh();

            Assert.That(board.Pool.VisibleCount, Is.GreaterThan(0), "hic pul cizilmedi");

            float height = ((RectTransform)board.transform).rect.height;
            float gap = MeasureCenterGap(board);
            float bandTop = (height * 0.5f) - (gap * 0.5f);
            float bandBottom = (height * 0.5f) + (gap * 0.5f);

            for (int i = 0; i < board.Pool.VisibleCount; i++)
            {
                // Pullarin pivotu merkezdedir (0.5, 0.5); anchoredPosition.y merkezi verir.
                RectTransform rect = board.Pool[i].Rect;
                float center = -rect.anchoredPosition.y;
                float half = rect.sizeDelta.y * 0.5f;
                float top = center - half;
                float bottom = center + half;

                Assert.That(bottom <= bandTop + 0.5f || top >= bandBottom - 0.5f, Is.True,
                            "pul " + i + " orta banda girmemeli (ust " + top + ", alt " + bottom +
                            ", bant " + bandTop + ".." + bandBottom + ")");
            }

            yield return null;
        }

        // ------------------------------------------------------------------
        // Yardimcilar
        // ------------------------------------------------------------------

        private static float MeasureCenterGap(BoardView board)
        {
            float topRowBottom = 0f;
            float bottomRowTop = float.MaxValue;

            for (int point = 1; point <= BoardLayout.PointCount; point++)
            {
                RectTransform rect = board.Points[point - 1].Rect;
                float top = -rect.anchoredPosition.y;
                float bottom = top + rect.sizeDelta.y;

                if (rect.anchoredPosition.y > -1f)
                {
                    topRowBottom = Mathf.Max(topRowBottom, bottom);
                }
                else
                {
                    bottomRowTop = Mathf.Min(bottomRowTop, top);
                }
            }

            return bottomRowTop - topRowBottom;
        }

        private static void AssertHalvesCoverFullHeight(RectTransform first, RectTransform second,
                                                        float height, string label)
        {
            float firstTop = -first.anchoredPosition.y;
            float firstBottom = firstTop + first.sizeDelta.y;
            float secondTop = -second.anchoredPosition.y;
            float secondBottom = secondTop + second.sizeDelta.y;

            float top = Mathf.Min(firstTop, secondTop);
            float bottom = Mathf.Max(firstBottom, secondBottom);

            Assert.That(top, Is.EqualTo(0f).Within(1f), label + " ustten baslamali (ust " + top + ")");
            Assert.That(bottom, Is.EqualTo(height).Within(1f),
                        label + " tam yukseklikte olmali (alt " + bottom + ", tahta " + height + ")");
            Assert.That(first.sizeDelta.y, Is.EqualTo(height * 0.5f).Within(1f),
                        label + " yarim yuksekligi (birinci)");
            Assert.That(second.sizeDelta.y, Is.EqualTo(height * 0.5f).Within(1f),
                        label + " yarim yuksekligi (ikinci)");
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
